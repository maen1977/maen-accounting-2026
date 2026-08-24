using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Maen.Accounting.App.Data;
using Maen.Accounting.Core.Models;
using Maen.Accounting.Core.Services;

namespace Maen.Accounting.App.Services;

public sealed record SyncResult(int TotalEntries, int Uploaded, int LocalWins, int RemoteWins, DateTimeOffset CompletedAtUtc);

public sealed class FirestoreSyncService
{
    private readonly HttpClient _httpClient;
    private readonly FirebaseOptions _options;
    private readonly AuthTokenProvider _tokenProvider;
    private readonly ProfitEntryRepository _repository;
    private readonly AppPreferencesService _preferences;
    private readonly DeviceIdentityService _deviceIdentity;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public FirestoreSyncService(
        HttpClient httpClient,
        FirebaseOptions options,
        AuthTokenProvider tokenProvider,
        ProfitEntryRepository repository,
        AppPreferencesService preferences,
        DeviceIdentityService deviceIdentity)
    {
        _httpClient = httpClient;
        _options = options;
        _tokenProvider = tokenProvider;
        _repository = repository;
        _preferences = preferences;
        _deviceIdentity = deviceIdentity;
    }

    public async Task<int> RestoreLegacyBackupIfEmptyAsync(
        AuthSession session,
        CancellationToken cancellationToken = default)
    {
        if (session.IsLocal)
        {
            return 0;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var local = await _repository.GetAllForSyncAsync(session.UserId);
            if (local.Count > 0)
            {
                return 0;
            }

            var legacyJson = await DownloadLegacyBackupAsync(session, cancellationToken);
            if (legacyJson is null)
            {
                return 0;
            }

            var imported = LegacyBackupParser.Parse(
                legacyJson,
                session.UserId,
                session.Email,
                _deviceIdentity.GetOrCreate(),
                DateTimeOffset.UtcNow,
                _preferences.StorageScope);
            if (imported.Entries.Count == 0)
            {
                return 0;
            }

            await _repository.UpsertManyAsync(session.UserId, imported.Entries);
            return imported.Entries.Count;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SyncResult> SyncAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var session = await _tokenProvider.GetValidSessionAsync(cancellationToken);
            if (session.IsLocal)
            {
                throw new InvalidOperationException(UiText.Get("T308"));
            }

            var local = await _repository.GetAllForSyncAsync(session.UserId);
            var remote = await DownloadAllAsync(session, cancellationToken);
            var plan = SyncMergeEngine.BuildPlan(session.UserId, local, remote);

            foreach (var entry in plan.EntriesToPush)
            {
                await UploadAsync(session, entry, cancellationToken);
            }

            // لا نعلن نجاح المزامنة إلا بعد إعادة قراءة البيانات من Firestore.
            // نجاح PATCH يؤكد قبول الطلب، وإعادة التنزيل تؤكد أن السجل أصبح قابلاً للقراءة من السحابة.
            var verifiedRemote = await DownloadAllAsync(session, cancellationToken);
            var verifiedById = verifiedRemote.ToDictionary(entry => entry.EntryId, StringComparer.Ordinal);
            foreach (var entry in plan.EntriesToPush)
            {
                if (!verifiedById.TryGetValue(entry.EntryId, out var remoteEntry) ||
                    remoteEntry.Version < entry.Version ||
                    remoteEntry.UpdatedAtUtc < entry.UpdatedAtUtc)
                {
                    throw new InvalidOperationException(
                        UiText.Format("T309", entry.EntryDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)));
                }
            }

            await _repository.UpsertManyAsync(session.UserId, plan.MergedEntries);
            return new SyncResult(verifiedRemote.Count, plan.EntriesToPush.Count, plan.LocalWins, plan.RemoteWins, DateTimeOffset.UtcNow);
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
            if (!response.IsSuccessStatusCode)
            {
                throw CreateFirestoreException(UiText.Get("T310"), response.StatusCode, payload);
            }
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("documents", out var documents))
            {
                foreach (var item in documents.EnumerateArray())
                {
                    result.Add(ParseDocument(item, session.UserId, _preferences.StorageScope));
                }
            }

            pageToken = document.RootElement.TryGetProperty("nextPageToken", out var token)
                ? token.GetString()
                : null;
        } while (!string.IsNullOrWhiteSpace(pageToken));

        return result;
    }

    private async Task<string?> DownloadLegacyBackupAsync(
        AuthSession session,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = UserIsolation.NormalizeEmail(session.Email);
        var uri = LegacyDocumentUri(normalizedEmail);
        using var request = CreateRequest(HttpMethod.Get, uri, session.IdToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Forbidden)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw CreateFirestoreException(UiText.Get("T862"), response.StatusCode, payload);
        }

        using var document = JsonDocument.Parse(payload);
        if (!document.RootElement.TryGetProperty("fields", out var fields)
            || !fields.TryGetProperty("entries", out var entriesField))
        {
            return null;
        }

        var entries = ConvertFirestoreValue(entriesField);
        if (entries is not JsonArray entriesArray || entriesArray.Count == 0)
        {
            return null;
        }

        var backup = new JsonObject
        {
            ["version"] = ReadIntegerField(fields, "version") ?? 2,
            ["backupEmail"] = ReadStringField(fields, "backupEmail") ?? normalizedEmail,
            ["accountScope"] = ReadStringField(fields, "accountScope") ?? _preferences.StorageScope,
            ["entries"] = entriesArray
        };
        return backup.ToJsonString();
    }

    private Uri LegacyDocumentUri(string normalizedEmail) => new(
        $"https://firestore.googleapis.com/v1/projects/{Uri.EscapeDataString(_options.ProjectId)}/" +
        $"databases/(default)/documents/profit_tracker_backups/{Uri.EscapeDataString(normalizedEmail)}");

    private static string? ReadStringField(JsonElement fields, string name) =>
        fields.TryGetProperty(name, out var field)
            && field.TryGetProperty("stringValue", out var value)
            ? value.GetString()
            : null;

    private static long? ReadIntegerField(JsonElement fields, string name) =>
        fields.TryGetProperty(name, out var field)
            && field.TryGetProperty("integerValue", out var value)
            && long.TryParse(value.GetString(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static JsonNode? ConvertFirestoreValue(JsonElement field)
    {
        if (field.TryGetProperty("stringValue", out var stringValue))
        {
            return JsonValue.Create(stringValue.GetString() ?? string.Empty);
        }

        if (field.TryGetProperty("integerValue", out var integerValue)
            && long.TryParse(integerValue.GetString(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var integer))
        {
            return JsonValue.Create(integer);
        }

        if (field.TryGetProperty("doubleValue", out var doubleValue)
            && doubleValue.TryGetDouble(out var number))
        {
            return JsonValue.Create(number);
        }

        if (field.TryGetProperty("booleanValue", out var booleanValue))
        {
            return JsonValue.Create(booleanValue.GetBoolean());
        }

        if (field.TryGetProperty("nullValue", out _))
        {
            return null;
        }

        if (field.TryGetProperty("timestampValue", out var timestampValue))
        {
            return JsonValue.Create(timestampValue.GetString() ?? string.Empty);
        }

        if (field.TryGetProperty("arrayValue", out var arrayValue))
        {
            var array = new JsonArray();
            if (arrayValue.TryGetProperty("values", out var values))
            {
                foreach (var value in values.EnumerateArray())
                {
                    array.Add(ConvertFirestoreValue(value));
                }
            }

            return array;
        }

        if (field.TryGetProperty("mapValue", out var mapValue))
        {
            var map = new JsonObject();
            if (mapValue.TryGetProperty("fields", out var fields))
            {
                foreach (var property in fields.EnumerateObject())
                {
                    map[property.Name] = ConvertFirestoreValue(property.Value);
                }
            }

            return map;
        }

        return null;
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
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            throw CreateFirestoreException(
                UiText.Format("T311", entry.EntryDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)),
                response.StatusCode,
                payload);
        }
    }

    private Uri CollectionUri(string userId, string? pageToken)
    {
        var uri = $"https://firestore.googleapis.com/v1/projects/{Uri.EscapeDataString(_options.ProjectId)}/" +
                  $"databases/(default)/documents/users/{Uri.EscapeDataString(userId)}/{CollectionName()}?pageSize=200";
        if (!string.IsNullOrWhiteSpace(pageToken))
        {
            uri += $"&pageToken={Uri.EscapeDataString(pageToken)}";
        }
        return new Uri(uri);
    }

    private Uri DocumentUri(string userId, string entryId) => new(
        $"https://firestore.googleapis.com/v1/projects/{Uri.EscapeDataString(_options.ProjectId)}/" +
        $"databases/(default)/documents/users/{Uri.EscapeDataString(userId)}/{CollectionName()}/{Uri.EscapeDataString(entryId)}");

    private string CollectionName() => _preferences.StorageScope == "personal" ? "personalEntries" : "entries";

    private static HttpRequestMessage CreateRequest(HttpMethod method, Uri uri, string idToken)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private Dictionary<string, object> ToFirestoreFields(ProfitEntry entry) => new()
    {
        ["accountScope"] = StringField(_preferences.StorageScope),
        ["entryId"] = StringField(entry.EntryId),
        ["userId"] = StringField(entry.UserId),
        ["entryDate"] = StringField(entry.EntryDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)),
        ["salesMinor"] = IntegerField(entry.SalesMinor),
        ["costMinor"] = IntegerField(entry.CostMinor),
        ["expensesMinor"] = IntegerField(entry.ExpensesMinor),
        ["notes"] = StringField(entry.Notes),
        ["isDeleted"] = new { booleanValue = entry.IsDeleted },
        ["createdAtUtc"] = StringField(entry.CreatedAtUtc.ToString("O")),
        ["updatedAtUtc"] = StringField(entry.UpdatedAtUtc.ToString("O")),
        ["version"] = IntegerField(entry.Version),
        ["deviceId"] = StringField(entry.DeviceId),
        ["amountMinor"] = IntegerField(entry.AmountMinor),
        ["movementType"] = StringField(entry.MovementType),
        ["category"] = StringField(entry.Category),
        ["wallet"] = StringField(entry.Wallet),
        ["counterparty"] = StringField(entry.Counterparty),
        ["integrityHash"] = StringField(entry.IntegrityHash)
    };

    private static object StringField(string value) => new { stringValue = value };
    private static object IntegerField(long value) => new { integerValue = value.ToString(System.Globalization.CultureInfo.InvariantCulture) };

    private static InvalidOperationException CreateFirestoreException(
        string operation,
        System.Net.HttpStatusCode statusCode,
        string payload)
    {
        if (statusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            return new InvalidOperationException(UiText.Get("T864"));
        }

        var detail = ExtractFirestoreError(payload);
        var suffix = string.IsNullOrWhiteSpace(detail) ? string.Empty : $": {detail}";
        return new InvalidOperationException(
            UiText.Format("T312", operation, (int)statusCode, suffix));
    }

    private static string ExtractFirestoreError(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return string.Empty;
        }

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

    private static ProfitEntry ParseDocument(JsonElement document, string expectedUserId, string expectedScope)
    {
        var fields = document.GetProperty("fields");
        string String(string name) => fields.GetProperty(name).GetProperty("stringValue").GetString() ?? string.Empty;
        string OptionalString(string name, string fallback = "") => fields.TryGetProperty(name, out var field) && field.TryGetProperty("stringValue", out var value)
            ? value.GetString() ?? fallback
            : fallback;
        var accountScope = fields.TryGetProperty("accountScope", out var scopeField)
            ? scopeField.GetProperty("stringValue").GetString() ?? string.Empty
            : "business";
        if (!string.Equals(accountScope, expectedScope, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(UiText.Get("T331"));
        }

        long Integer(string name) => long.Parse(fields.GetProperty(name).GetProperty("integerValue").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        long OptionalInteger(string name, long fallback) => fields.TryGetProperty(name, out var field) && field.TryGetProperty("integerValue", out var value)
            ? long.Parse(value.GetString()!, System.Globalization.CultureInfo.InvariantCulture)
            : fallback;
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
            DateTimeOffset.Parse(String("createdAtUtc"), System.Globalization.CultureInfo.InvariantCulture),
            DateTimeOffset.Parse(String("updatedAtUtc"), System.Globalization.CultureInfo.InvariantCulture),
            checked((int)Integer("version")),
            String("deviceId"),
            OptionalInteger("amountMinor", checked(Integer("salesMinor") + Integer("costMinor") + Integer("expensesMinor"))),
            OptionalString("movementType", Integer("salesMinor") > 0 ? PersonalMovementTypes.OtherIncome : Integer("costMinor") > 0 || Integer("expensesMinor") > 0 ? PersonalMovementTypes.Purchase : PersonalMovementTypes.Other),
            OptionalString("category"),
            OptionalString("wallet", "main"),
            OptionalString("counterparty"),
            OptionalString("integrityHash"));
    }
}
