using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Maen.Accounting.Core.Models;
using Maen.Accounting.Core.Services;

namespace Maen.Accounting.App.Services;

public sealed class FirebaseAuthService
{
    private readonly HttpClient _httpClient;
    private readonly FirebaseOptions _options;
    private readonly DeviceIdentityService _deviceIdentityService;

    public FirebaseAuthService(
        HttpClient httpClient,
        FirebaseOptions options,
        DeviceIdentityService deviceIdentityService)
    {
        _httpClient = httpClient;
        _options = options;
        _deviceIdentityService = deviceIdentityService;
    }

    public Task<AuthSession> SignInAsync(string email, string password, CancellationToken cancellationToken = default) =>
        AuthenticateAsync("accounts:signInWithPassword", email, password, cancellationToken);

    public Task<AuthSession> RegisterAsync(string email, string password, CancellationToken cancellationToken = default) =>
        AuthenticateAsync("accounts:signUp", email, password, cancellationToken);

    public async Task SendPasswordResetEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalized = UserIsolation.NormalizeEmail(email);
        using var response = await _httpClient.PostAsJsonAsync(
            $"https://identitytoolkit.googleapis.com/v1/accounts:sendOobCode?key={Uri.EscapeDataString(_options.ApiKey)}",
            new
            {
                requestType = "PASSWORD_RESET",
                email = normalized
            },
            JsonOptions,
            cancellationToken);

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var code = ReadFirebaseErrorCode(payload);
        if (code == "EMAIL_NOT_FOUND")
        {
            // Do not reveal whether an account exists. The UI shows the same message either way.
            return;
        }

        throw new InvalidOperationException(MapFirebaseError(payload));
    }

    public async Task<AuthSession> RefreshAsync(AuthSession current, CancellationToken cancellationToken = default)
    {
        if (current.IsLocal)
        {
            return current;
        }

        using var response = await _httpClient.PostAsync(
            $"https://securetoken.googleapis.com/v1/token?key={Uri.EscapeDataString(_options.ApiKey)}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = current.RefreshToken
            }),
            cancellationToken);

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(MapFirebaseError(payload));
        }

        var dto = JsonSerializer.Deserialize<RefreshResponse>(payload, JsonOptions)
            ??             throw new InvalidOperationException(UiText.Get("T238"));
        return current with
        {
            UserId = dto.UserId,
            IdToken = dto.IdToken,
            RefreshToken = dto.RefreshToken,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(ParseLifetime(dto.ExpiresIn))
        };
    }

    public AuthSession CreateLocalDeviceSession()
    {
        var deviceId = _deviceIdentityService.GetOrCreate();
        return new AuthSession(
            UserIsolation.LocalDeviceUserId(deviceId),
            UiText.Get("T240"),
            string.Empty,
            string.Empty,
            DateTimeOffset.MaxValue,
            true);
    }

    // Kept for compatibility with previously exported local sessions.
    public AuthSession CreateLocalSession(string email)
    {
        var normalized = UserIsolation.NormalizeEmail(email);
        return new AuthSession(
            UserIsolation.LocalUserId(normalized),
            normalized,
            string.Empty,
            string.Empty,
            DateTimeOffset.MaxValue,
            true);
    }

    private async Task<AuthSession> AuthenticateAsync(
        string action,
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var normalized = UserIsolation.NormalizeEmail(email);
        if (password.Length < 6)
        {
            throw new InvalidOperationException(UiText.Get("T229"));
        }

        using var response = await _httpClient.PostAsJsonAsync(
            $"https://identitytoolkit.googleapis.com/v1/{action}?key={Uri.EscapeDataString(_options.ApiKey)}",
            new { email = normalized, password, returnSecureToken = true },
            JsonOptions,
            cancellationToken);

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(MapFirebaseError(payload));
        }

        var dto = JsonSerializer.Deserialize<AuthResponse>(payload, JsonOptions)
            ?? throw new InvalidOperationException(UiText.Get("T239"));
        return new AuthSession(
            dto.LocalId,
            dto.Email ?? normalized,
            dto.IdToken,
            dto.RefreshToken,
            DateTimeOffset.UtcNow.AddSeconds(ParseLifetime(dto.ExpiresIn)),
            false);
    }

    private static int ParseLifetime(string? expiresIn) =>
        int.TryParse(expiresIn, out var seconds) ? Math.Max(seconds, 60) : 3600;

    private static string MapFirebaseError(string payload)
    {
        return ReadFirebaseErrorCode(payload) switch
        {
            "EMAIL_EXISTS" => UiText.Get("T230"),
            "INVALID_LOGIN_CREDENTIALS" or "INVALID_PASSWORD" => UiText.Get("T231"),
            "EMAIL_NOT_FOUND" => UiText.Get("T232"),
            "INVALID_EMAIL" => UiText.Get("T233"),
            "WEAK_PASSWORD" => UiText.Get("T234"),
            "USER_DISABLED" => UiText.Get("T235"),
            "TOO_MANY_ATTEMPTS_TRY_LATER" => UiText.Get("T236"),
            "OPERATION_NOT_ALLOWED" or "API_KEY_INVALID" or "INVALID_API_KEY" or "PROJECT_NOT_FOUND" => UiText.Get("T861"),
            _ => UiText.Get("T237")
        };
    }

    private static string ReadFirebaseErrorCode(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var message = document.RootElement
                .GetProperty("error")
                .GetProperty("message")
                .GetString() ?? string.Empty;
            return message.Split(':', 2)[0];
        }
        catch
        {
            return string.Empty;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record AuthResponse(
        string LocalId,
        string? Email,
        string IdToken,
        string RefreshToken,
        string ExpiresIn);

    private sealed record RefreshResponse(
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("id_token")] string IdToken,
        [property: JsonPropertyName("refresh_token")] string RefreshToken,
        [property: JsonPropertyName("expires_in")] string ExpiresIn);
}
