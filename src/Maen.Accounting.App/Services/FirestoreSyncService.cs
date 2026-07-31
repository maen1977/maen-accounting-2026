using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Maen.Accounting.App.Data;
using Maen.Accounting.Core.Models;
using Maen.Accounting.Core.Services;

namespace Maen.Accounting.App.Services;

public sealed record SyncResult(int TotalEntries, int Uploaded, int RemoteWins, DateTimeOffset CompletedAtUtc);

public sealed class FirestoreSyncService
{
    private readonly HttpClient _httpClient;
    private readonly FirebaseOptions _options;
    private readonly AuthTokenProvider _tokenProvider;
    private readonly ProfitEntryRepository _repository;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public FirestoreSyncService(
        HttpClient httpClient,
        FirebaseOptions options,
        AuthTokenProvider tokenProvider,
        ProfitEntryRepository repository)
    {
        _httpClient = httpClient;
        _options = options;
        _tokenProvider = tokenProvider;
        _repository = repository;
    }

    public async Task<SyncResult> SyncAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var session = await _tokenProvider.GetValidSessionAsync(cancellationToken);
            if (session.IsLocal)
            {
                throw new InvalidOperationException("الوضع المحلي لا يستخدم المزامنة السحابية.");
            }

            var local = await _repository.GetAllForSyncAsync(session.UserId);
            var remote = await DownloadAllAsync(session, cancellationToken);
            var plan = SyncMergeEngine.BuildPlan(session.UserId, local, remote);

            foreach (var entry in plan.EntriesToPush)
            {
                await UploadAsync(session, entry, cancellationToken);
            }

            await _repository.UpsertManyAsync(session.UserId, plan.MergedEntries);
            return new SyncResult(plan.MergedEntries.Count, plan.EntriesToPush.Count, plan.RemoteWins, DateTimeOffset.UtcNow);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<ProfitEntry>> DownloadAllAsync(
        AuthSession session,
        CancellationToken cancellationToken)
    {
        var result = new List<ProfitEntry>();
        string? pageToken = null;
        do
        {
            var uri = CollectionUri(session.UserId, pageToken);
            using var request = CreateRequest(HttpMethod.Get, uri, session.IdToken);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return result;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"تعذر تنزيل البيانات السحابية (رمز {(int)response.StatusCode}).");
            }
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("documents", out var documents))
            {
                foreach (var item in documents.EnumerateArray())
                {
                    result.Add(ParseDocument(item, session.UserId));
                }
            }

            pageToken = document.RootElement.TryGetProperty("nextPageToken", out var token)
                ? token.GetString()
                : null;
        } while (!string.IsNullOrWhiteSpace(pageToken));

        return result;
    }

    private async Task UploadAsync(
        AuthSession session,
        ProfitEntry entry,
        CancellationToken cancellationToken)
    {
        UserIsolation.EnsureOwner(session.UserId, entry.UserId);
        var uri = DocumentUri(session.UserId, entry.EntryId);
        using var request = CreateRequest(HttpMethod.Patch, uri, session.IdToken);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { fields = ToFirestoreFields(entry) }),
            Encoding.UTF8,
            "application/json");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"فشلت مزامنة سجل {entry.EntryDate:yyyy-MM-dd} (رمز {(int)response.StatusCode}).");
        }
    }

    private Uri CollectionUri(string userId, string? pageToken)
    {
        var uri = $"https://firestore.googleapis.com/v1/projects/{Uri.EscapeDataString(_options.ProjectId)}/" +
                  $"databases/(default)/documents/users/{Uri.EscapeDataString(userId)}/entries?pageSize=200";
        if (!string.IsNullOrWhiteSpace(pageToken))
        {
            uri += $"&pageToken={Uri.EscapeDataString(pageToken)}";
        }
        return new Uri(uri);
    }

    private Uri DocumentUri(string userId, string entryId) => new(
        $"https://firestore.googleapis.com/v1/projects/{Uri.EscapeDataString(_options.ProjectId)}/" +
        $"databases/(default)/documents/users/{Uri.EscapeDataString(userId)}/entries/{Uri.EscapeDataString(entryId)}");

    private static HttpRequestMessage CreateRequest(HttpMethod method, Uri uri, string idToken)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static Dictionary<string, object> ToFirestoreFields(ProfitEntry entry) => new()
    {
        ["entryId"] = StringField(entry.EntryId),
        ["userId"] = StringField(entry.UserId),
        ["entryDate"] = StringField(entry.EntryDate.ToString("yyyy-MM-dd")),
        ["salesMinor"] = IntegerField(entry.SalesMinor),
        ["costMinor"] = IntegerField(entry.CostMinor),
        ["expensesMinor"] = IntegerField(entry.ExpensesMinor),
        ["notes"] = StringField(entry.Notes),
        ["isDeleted"] = new { booleanValue = entry.IsDeleted },
        ["createdAtUtc"] = StringField(entry.CreatedAtUtc.ToString("O")),
        ["updatedAtUtc"] = StringField(entry.UpdatedAtUtc.ToString("O")),
        ["version"] = IntegerField(entry.Version),
        ["deviceId"] = StringField(entry.DeviceId)
    };

    private static object StringField(string value) => new { stringValue = value };
    private static object IntegerField(long value) => new { integerValue = value.ToString(System.Globalization.CultureInfo.InvariantCulture) };

    private static ProfitEntry ParseDocument(JsonElement document, string expectedUserId)
    {
        var fields = document.GetProperty("fields");
        string String(string name) => fields.GetProperty(name).GetProperty("stringValue").GetString() ?? string.Empty;
        long Integer(string name) => long.Parse(fields.GetProperty(name).GetProperty("integerValue").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        bool Boolean(string name) => fields.GetProperty(name).GetProperty("booleanValue").GetBoolean();

        var userId = String("userId");
        UserIsolation.EnsureOwner(expectedUserId, userId);
        return new ProfitEntry(
            String("entryId"),
            userId,
            DateOnly.ParseExact(String("entryDate"), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            Integer("salesMinor"),
            Integer("costMinor"),
            Integer("expensesMinor"),
            String("notes"),
            Boolean("isDeleted"),
            DateTimeOffset.Parse(String("createdAtUtc")),
            DateTimeOffset.Parse(String("updatedAtUtc")),
            checked((int)Integer("version")),
            String("deviceId"));
    }
}
