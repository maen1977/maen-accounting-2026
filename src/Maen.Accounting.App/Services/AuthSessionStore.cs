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

    public async Task SaveAsync(AuthSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        try
        {
            await SecureStorage.Default.SetAsync(SessionKey, JsonSerializer.Serialize(session, JsonOptions));
        }
        catch when (session.IsLocal)
        {
            // SecureStorage can be unavailable during the first Android activity
            // transition. Local access does not need a cloud token, so it must still
            // open the app; the user can choose local access again on the next launch.
        }
    }

    public Task ClearAsync()
    {
        SecureStorage.Default.Remove(SessionKey);
        return Task.CompletedTask;
    }
}
