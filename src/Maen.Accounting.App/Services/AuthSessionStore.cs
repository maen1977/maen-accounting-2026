using System.Text.Json;
using Maen.Accounting.Core.Models;

namespace Maen.Accounting.App.Services;

public sealed class AuthSessionStore
{
    private const string SessionKey = "maen_auth_session_v2";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AuthSession?> LoadAsync()
    {
        try
        {
            var json = await SecureStorage.Default.GetAsync(SessionKey);
            return string.IsNullOrWhiteSpace(json)
                ? null
                : JsonSerializer.Deserialize<AuthSession>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public Task SaveAsync(AuthSession session) =>
        SecureStorage.Default.SetAsync(SessionKey, JsonSerializer.Serialize(session, JsonOptions));

    public Task ClearAsync()
    {
        SecureStorage.Default.Remove(SessionKey);
        return Task.CompletedTask;
    }
}
