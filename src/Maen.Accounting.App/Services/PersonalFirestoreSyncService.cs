using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Maen.Accounting.App.Data;
using Maen.Accounting.Core.Models;
using Maen.Accounting.Core.Services;

namespace Maen.Accounting.App.Services;

/// <summary>
/// Synchronizes personal planning documents (financial plans, obligations, deposits)
/// to Firestore with the same merge contract as the business entity sync service.
/// </summary>
public sealed record PersonalEntitySyncResult(
    int PlansTotal,
    int PlansUploaded,
    int PlansLocalWins,
    int PlansRemoteWins,
    int ObligationsTotal,
    int ObligationsUploaded,
    int ObligationsLocalWins,
    int ObligationsRemoteWins,
    int DepositsTotal,
    int DepositsUploaded,
    int DepositsLocalWins,
    int DepositsRemoteWins,
    DateTimeOffset CompletedAtUtc)
{
    public int TotalRecords => PlansTotal + ObligationsTotal + DepositsTotal;
    public int Uploaded => PlansUploaded + ObligationsUploaded + DepositsUploaded;
    public int LocalWins => PlansLocalWins + ObligationsLocalWins + DepositsLocalWins;
    public int RemoteWins => PlansRemoteWins + ObligationsRemoteWins + DepositsRemoteWins;
}

public sealed class PersonalFirestoreSyncService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly FirebaseOptions _options;
    private readonly AuthTokenProvider _tokenProvider;
    private readonly PlanningRepository _repository;
    private readonly AppPreferencesService _preferences;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public PersonalFirestoreSyncService(
        HttpClient httpClient,
        FirebaseOptions options,
        AuthTokenProvider tokenProvider,
        PlanningRepository repository,
        AppPreferencesService preferences)
    {
        _httpClient = httpClient;
        _options = options;
        _tokenProvider = tokenProvider;
        _repository = repository;
        _preferences = preferences;
    }

    public async Task<PersonalEntitySyncResult> SyncAsync(CancellationToken cancellationToken = default)
    {
        if (!string.Equals(_preferences.StorageScope, "personal", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Personal entity synchronization requires personal account scope.");
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var session = await _tokenProvider.GetValidSessionAsync(cancellationToken);
            if (session.IsLocal)
            {
                throw new InvalidOperationException(UiText.Get("T308"));
            }

            var plans = await _repository.GetPlansAsync(session.UserId);
            var obligations = await _repository.GetObligationsAsync(session.UserId);
            var deposits = await _repository.GetDepositsAsync(session.UserId);

            var planStats = await SyncEntityAsync(
                session,
                plans,
                "personalPlans",
                "plan",
                static row => row.PlanId,
                static row => new DateTimeOffset(row.UpdatedAtUtcTicks, TimeSpan.Zero),
                static row => row.Version,
                static row => row.DeviceId,
                merged => _repository.UpsertPlansAsync(session.UserId, merged),
                cancellationToken);

            var obligationStats = await SyncEntityAsync(
                session,
                obligations,
                "personalObligations",
                "obligation",
                static row => row.ObligationId,
                static row => new DateTimeOffset(row.UpdatedAtUtcTicks, TimeSpan.Zero),
                static row => row.Version,
                static row => row.DeviceId,
                merged => _repository.UpsertObligationsAsync(session.UserId, merged),
                cancellationToken);

            var depositStats = await SyncEntityAsync(
                session,
                deposits,
                "personalDeposits",
                "deposit",
                static row => row.DepositId,
                static row => new DateTimeOffset(row.UpdatedAtUtcTicks, TimeSpan.Zero),
                static row => row.Version,
                static row => row.DeviceId,
                merged => _repository.UpsertDepositsAsync(session.UserId, merged),
                cancellationToken);

            return new PersonalEntitySyncResult(
                planStats.Total, planStats.Uploaded, planStats.LocalWins, planStats.RemoteWins,
                obligationStats.Total, obligationStats.Uploaded, obligationStats.LocalWins, obligationStats.RemoteWins,
                depositStats.Total, depositStats.Uploaded, depositStats.LocalWins, depositStats.RemoteWins,
                DateTimeOffset.UtcNow);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<EntitySyncStats> SyncEntityAsync<T>(
        AuthSession session,
        IReadOnlyList<T> local,
        string collection,
        string entityType,
        Func<T, string> idSelector,
        Func<T, DateTimeOffset> updatedAtSelector,
        Func<T, int> versionSelector,
        Func<T, string> deviceSelector,
        Func<IReadOnlyList<T>, Task> saveMerged,
        CancellationToken cancellationToken)
    {
        var remote = await DownloadAsync(session, collection, entityType, idSelector, cancellationToken);
        var plan = SyncDocumentMergeEngine.BuildPlan(
            session.UserId,
            local,
            remote,
            idSelector,
            static (T item) => ReadUserId<T>(item),
            updatedAtSelector,
            versionSelector,
            deviceSelector);

        foreach (var item in plan.ItemsToPush)
        {
            await UploadAsync(session, collection, entityType, item, idSelector, updatedAtSelector, versionSelector, deviceSelector, cancellationToken);
        }

        var verifiedRemote = await DownloadAsync(session, collection, entityType, idSelector, cancellationToken);
        var verifiedById = verifiedRemote.ToDictionary(idSelector, StringComparer.Ordinal);
        foreach (var item in plan.ItemsToPush)
        {
            var id = idSelector(item);
            if (!verifiedById.TryGetValue(id, out var remoteItem) ||
                versionSelector(remoteItem) < versionSelector(item) ||
                updatedAtSelector(remoteItem) < updatedAtSelector(item))
            {
                throw new InvalidOperationException(UiText.Format("T309", id));
            }
        }

        await saveMerged(plan.MergedItems);
        return new EntitySyncStats(verifiedRemote.Count, plan.ItemsToPush.Count, plan.LocalWins, plan.RemoteWins);
    }

    private async Task<IReadOnlyList<T>> DownloadAsync<T>(
        AuthSession session,
        string collection,
        string entityType,
        Func<T, string> idSelector,
        CancellationToken cancellationToken)
    {
        var result = new List<T>();
        string? pageToken = null;
        do
        {
            var uri = CollectionUri(session.UserId, collection, pageToken);
            using var request = CreateRequest(HttpMethod.Get, uri, session.IdToken);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw CreateFirestoreException(UiText.Get("T310"), response.StatusCode, payload);
            }

            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("documents", out var documents))
            {
                foreach (var item in documents.EnumerateArray())
                {
                    result.Add(ParseDocument<T>(item, session.UserId, entityType, idSelector));
                }
            }

            pageToken = document.RootElement.TryGetProperty("nextPageToken", out var token)
                ? token.GetString()
                : null;
        } while (!string.IsNullOrWhiteSpace(pageToken));

        return result;
    }

    private async Task UploadAsync<T>(
        AuthSession session,
        string collection,
        string entityType,
        T item,
        Func<T, string> idSelector,
        Func<T, DateTimeOffset> updatedAtSelector,
        Func<T, int> versionSelector,
        Func<T, string> deviceSelector,
        CancellationToken cancellationToken)
    {
        var id = idSelector(item);
        UserIsolation.EnsureOwner(session.UserId, ReadUserId(item));
        var uri = DocumentUri(session.UserId, collection, id);
        var fields = new Dictionary<string, object>
        {
            ["accountScope"] = StringField("personal"),
            ["entityType"] = StringField(entityType),
            ["recordId"] = StringField(id),
            ["userId"] = StringField(ReadUserId(item)),
            ["updatedAtUtc"] = StringField(updatedAtSelector(item).ToString("O", CultureInfo.InvariantCulture)),
            ["version"] = IntegerField(versionSelector(item)),
            ["deviceId"] = StringField(deviceSelector(item)),
            ["payload"] = StringField(JsonSerializer.Serialize(item, JsonOptions))
        };

        using var request = CreateRequest(HttpMethod.Patch, uri, session.IdToken);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { fields }),
            Encoding.UTF8,
            "application/json");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            throw CreateFirestoreException(UiText.Format("T311", id), response.StatusCode, payload);
        }
    }

    private Uri CollectionUri(string userId, string collection, string? pageToken)
    {
        var uri = $"https://firestore.googleapis.com/v1/projects/{Uri.EscapeDataString(_options.ProjectId)}/" +
                  $"databases/(default)/documents/users/{Uri.EscapeDataString(userId)}/{collection}?pageSize=200";
        if (!string.IsNullOrWhiteSpace(pageToken))
        {
            uri += $"&pageToken={Uri.EscapeDataString(pageToken)}";
        }

        return new Uri(uri);
    }

    private Uri DocumentUri(string userId, string collection, string recordId) => new(
        $"https://firestore.googleapis.com/v1/projects/{Uri.EscapeDataString(_options.ProjectId)}/" +
        $"databases/(default)/documents/users/{Uri.EscapeDataString(userId)}/{collection}/{Uri.EscapeDataString(recordId)}");

    private static HttpRequestMessage CreateRequest(HttpMethod method, Uri uri, string idToken)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static T ParseDocument<T>(
        JsonElement document,
        string expectedUserId,
        string expectedEntityType,
        Func<T, string> idSelector)
    {
        var fields = document.GetProperty("fields");
        var accountScope = ReadString(fields, "accountScope", "personal");
        if (!string.Equals(accountScope, "personal", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(UiText.Get("T331"));
        }

        var entityType = ReadString(fields, "entityType");
        if (!string.Equals(entityType, expectedEntityType, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(UiText.Get("T331"));
        }

        var userId = ReadString(fields, "userId");
        UserIsolation.EnsureOwner(expectedUserId, userId);
        var payload = ReadString(fields, "payload");
        var item = JsonSerializer.Deserialize<T>(payload, JsonOptions)
            ?? throw new InvalidOperationException("A synchronized personal record could not be decoded.");
        UserIsolation.EnsureOwner(expectedUserId, ReadUserId(item));

        var recordId = ReadString(fields, "recordId");
        if (!string.Equals(recordId, idSelector(item), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A synchronized personal record has inconsistent identifiers.");
        }

        return item;
    }

    private static string ReadString(JsonElement fields, string name, string fallback = "") =>
        fields.TryGetProperty(name, out var field) && field.TryGetProperty("stringValue", out var value)
            ? value.GetString() ?? fallback
            : fallback;

    private static object StringField(string value) => new { stringValue = value };

    private static object IntegerField(int value) => new
    {
        integerValue = value.ToString(CultureInfo.InvariantCulture)
    };

    private static string ReadUserId<T>(T item) => item switch
    {
        FinancialPlanRow row => row.UserId,
        ObligationRow row => row.UserId,
        DepositRow row => row.UserId,
        _ => throw new InvalidOperationException("Unsupported synchronized personal record type.")
    };

    private static InvalidOperationException CreateFirestoreException(
        string operation,
        System.Net.HttpStatusCode statusCode,
        string payload)
    {
        var detail = ExtractFirestoreError(payload);
        var suffix = string.IsNullOrWhiteSpace(detail) ? string.Empty : $": {detail}";
        return new InvalidOperationException(
            UiText.Format("T312", string.IsNullOrWhiteSpace(operation) ? "sync" : operation, (int)statusCode, suffix));
    }

    private static string ExtractFirestoreError(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return string.Empty;

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("error", out var error))
            {
                if (error.TryGetProperty("message", out var message))
                {
                    return message.GetString() ?? string.Empty;
                }

                if (error.TryGetProperty("status", out var status))
                {
                    return status.GetString() ?? string.Empty;
                }
            }
        }
        catch (JsonException)
        {
            // تُعاد رسالة HTTP العامة عندما لا يكون الرد بصيغة JSON.
        }

        return string.Empty;
    }

    private sealed record EntitySyncStats(int Total, int Uploaded, int LocalWins, int RemoteWins);
}
