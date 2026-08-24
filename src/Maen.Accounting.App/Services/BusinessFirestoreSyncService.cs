using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Maen.Accounting.App.Data;
using Maen.Accounting.Core.Models;
using Maen.Accounting.Core.Services;
using AccountingContact = Maen.Accounting.Core.Models.Contact;

namespace Maen.Accounting.App.Services;

public sealed record BusinessSyncResult(
    int ContactsTotal,
    int ContactsUploaded,
    int ContactsLocalWins,
    int ContactsRemoteWins,
    int InvoicesTotal,
    int InvoicesUploaded,
    int InvoicesLocalWins,
    int InvoicesRemoteWins,
    int PaymentsTotal,
    int PaymentsUploaded,
    int PaymentsLocalWins,
    int PaymentsRemoteWins,
    DateTimeOffset CompletedAtUtc)
{
    public int TotalRecords => ContactsTotal + InvoicesTotal + PaymentsTotal;
    public int Uploaded => ContactsUploaded + InvoicesUploaded + PaymentsUploaded;
    public int LocalWins => ContactsLocalWins + InvoicesLocalWins + PaymentsLocalWins;
    public int RemoteWins => ContactsRemoteWins + InvoicesRemoteWins + PaymentsRemoteWins;
}

public sealed class BusinessFirestoreSyncService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly FirebaseOptions _options;
    private readonly AuthTokenProvider _tokenProvider;
    private readonly BusinessRepository _repository;
    private readonly AppPreferencesService _preferences;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public BusinessFirestoreSyncService(
        HttpClient httpClient,
        FirebaseOptions options,
        AuthTokenProvider tokenProvider,
        BusinessRepository repository,
        AppPreferencesService preferences)
    {
        _httpClient = httpClient;
        _options = options;
        _tokenProvider = tokenProvider;
        _repository = repository;
        _preferences = preferences;
    }

    public async Task<BusinessSyncResult> SyncAsync(CancellationToken cancellationToken = default)
    {
        if (!string.Equals(_preferences.StorageScope, "business", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Business synchronization requires business account scope.");
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var session = await _tokenProvider.GetValidSessionAsync(cancellationToken);
            if (session.IsLocal)
            {
                throw new InvalidOperationException(UiText.Get("T308"));
            }

            var contacts = await _repository.GetContactsForSyncAsync(session.UserId);
            var invoices = await _repository.GetInvoicesAsync(session.UserId);
            var payments = await _repository.GetPaymentsAsync(session.UserId);

            var contactStats = await SyncEntityAsync(
                session,
                contacts,
                collection: "businessContacts",
                entityType: "contact",
                idSelector: static contact => contact.ContactId,
                updatedAtSelector: static contact => contact.UpdatedAtUtc ?? DateTimeOffset.MinValue,
                versionSelector: static contact => contact.Version,
                deviceSelector: static contact => contact.DeviceId,
                saveMerged: merged => _repository.UpsertContactsFromSyncAsync(session.UserId, merged),
                cancellationToken);

            var invoiceStats = await SyncEntityAsync(
                session,
                invoices,
                collection: "businessInvoices",
                entityType: "invoice",
                idSelector: static invoice => invoice.InvoiceId,
                updatedAtSelector: static invoice => invoice.UpdatedAtUtc ?? DateTimeOffset.MinValue,
                versionSelector: static invoice => invoice.Version,
                deviceSelector: static invoice => invoice.DeviceId,
                saveMerged: merged => _repository.UpsertInvoicesFromSyncAsync(session.UserId, merged),
                cancellationToken);

            var paymentStats = await SyncEntityAsync(
                session,
                payments,
                collection: "businessPayments",
                entityType: "payment",
                idSelector: static payment => payment.PaymentId,
                updatedAtSelector: static payment => payment.UpdatedAtUtc ?? DateTimeOffset.MinValue,
                versionSelector: static payment => payment.Version,
                deviceSelector: static payment => payment.DeviceId,
                saveMerged: merged => _repository.UpsertPaymentsFromSyncAsync(session.UserId, merged),
                cancellationToken);

            return new BusinessSyncResult(
                contactStats.Total,
                contactStats.Uploaded,
                contactStats.LocalWins,
                contactStats.RemoteWins,
                invoiceStats.Total,
                invoiceStats.Uploaded,
                invoiceStats.LocalWins,
                invoiceStats.RemoteWins,
                paymentStats.Total,
                paymentStats.Uploaded,
                paymentStats.LocalWins,
                paymentStats.RemoteWins,
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
        var remote = await DownloadAsync(
            session,
            collection,
            entityType,
            idSelector,
            cancellationToken);
        var plan = SyncDocumentMergeEngine.BuildPlan(
            session.UserId,
            local,
            remote,
            idSelector,
            static item => GetUserId(item),
            updatedAtSelector,
            versionSelector,
            deviceSelector);

        foreach (var item in plan.ItemsToPush)
        {
            await UploadAsync(
                session,
                collection,
                entityType,
                item,
                idSelector,
                updatedAtSelector,
                versionSelector,
                deviceSelector,
                cancellationToken);
        }

        var verifiedRemote = await DownloadAsync(
            session,
            collection,
            entityType,
            idSelector,
            cancellationToken);
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
                    var parsed = ParseDocument<T>(item, session.UserId, entityType, idSelector);
                    result.Add(parsed);
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
        UserIsolation.EnsureOwner(session.UserId, GetUserId(item));
        var uri = DocumentUri(session.UserId, collection, id);
        var fields = new Dictionary<string, object>
        {
            ["accountScope"] = StringField("business"),
            ["entityType"] = StringField(entityType),
            ["recordId"] = StringField(id),
            ["userId"] = StringField(GetUserId(item)),
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
        var accountScope = ReadString(fields, "accountScope", "business");
        if (!string.Equals(accountScope, "business", StringComparison.Ordinal))
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
            ?? throw new InvalidOperationException("A synchronized business record could not be decoded.");
        UserIsolation.EnsureOwner(expectedUserId, GetUserId(item));

                var recordId = ReadString(fields, "recordId");
        if (!string.Equals(recordId, idSelector(item), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A synchronized business record has inconsistent identifiers.");
        }
        VerifyDownloadedIntegrity(item);
        return item;
    }

    private static void VerifyDownloadedIntegrity<T>(T item)
    {
        var type = typeof(T);
        var hashProperty = type.GetProperty("IntegrityHash");
        if (hashProperty is null)
        {
            return;
        }

        var storedHash = hashProperty.GetValue(item) as string;
        var expectedHash = type switch
        {
            _ when type == typeof(Invoice) => ComputeExpectedHash<Invoice>(item as Invoice),
            _ when type == typeof(Payment) => ComputeExpectedHash<Payment>(item as Payment),
            _ => null
        };

        if (expectedHash is null || string.IsNullOrWhiteSpace(expectedHash)
            || string.IsNullOrWhiteSpace(storedHash))
        {
            return;
        }

        if (!DataIntegrityService.Verify(storedHash, expectedHash))
        {
            throw new InvalidOperationException(
                "A synchronized business record failed its integrity check and has been rejected.");
        }
    }

    private static string? ComputeExpectedHash<TDocument>(TDocument? document) => document switch
    {
        Invoice invoice => DataIntegrityService.ComputeInvoiceHash(
            invoice.InvoiceId, invoice.UserId, invoice.Version, (int)invoice.Type,
            invoice.TotalMinor, invoice.TaxMinor, (int)invoice.Status),
        Payment payment => DataIntegrityService.ComputePaymentHash(
            payment.PaymentId, payment.UserId, payment.Version, (int)payment.Type,
            payment.AmountMinor, payment.IsDeleted),
        _ => null
    };

    private static string ReadString(JsonElement fields, string name, string fallback = "") =>
        fields.TryGetProperty(name, out var field) && field.TryGetProperty("stringValue", out var value)
            ? value.GetString() ?? fallback
            : fallback;

    private static object StringField(string value) => new { stringValue = value };

    private static object IntegerField(int value) => new
    {
        integerValue = value.ToString(CultureInfo.InvariantCulture)
    };

    private static string GetUserId<T>(T item) => item switch
    {
        AccountingContact contact => contact.UserId,
        Invoice invoice => invoice.UserId,
        Payment payment => payment.UserId,
        _ => throw new InvalidOperationException("Unsupported synchronized business record type.")
    };

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
