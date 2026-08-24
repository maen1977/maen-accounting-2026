using Maen.Accounting.Core.Models;

namespace Maen.Accounting.App.Services;

public sealed class AuthTokenProvider
{
    private readonly AuthSessionStore _store;
    private readonly FirebaseAuthService _authService;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AuthTokenProvider(AuthSessionStore store, FirebaseAuthService authService)
    {
        _store = store;
        _authService = authService;
    }

    public async Task<AuthSession> GetValidSessionAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var session = await _store.LoadAsync()
                ?? throw new InvalidOperationException(UiText.Get("T313"));
            if (!session.NeedsRefresh(DateTimeOffset.UtcNow))
            {
                return session;
            }

            session = await _authService.RefreshAsync(session, cancellationToken);
            try
            {
                await _store.SaveAsync(session);
            }
            catch (Exception exception)
            {
                // A transient Android SecureStorage failure must not discard a valid refreshed token.
                // The current sync can continue in memory; the next app launch can ask the user to sign in again.
                System.Diagnostics.Debug.WriteLine($"[AuthSession] Refreshed token could not be persisted: {exception}");
            }

            return session;
        }
        finally
        {
            _gate.Release();
        }
    }
}
