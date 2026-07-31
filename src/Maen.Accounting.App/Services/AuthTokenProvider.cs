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
                ?? throw new InvalidOperationException("انتهت جلسة المستخدم.");
            if (!session.NeedsRefresh(DateTimeOffset.UtcNow))
            {
                return session;
            }

            session = await _authService.RefreshAsync(session, cancellationToken);
            await _store.SaveAsync(session);
            return session;
        }
        finally
        {
            _gate.Release();
        }
    }
}
