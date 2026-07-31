namespace Maen.Accounting.Core.Models;

public sealed record AuthSession(
    string UserId,
    string Email,
    string IdToken,
    string RefreshToken,
    DateTimeOffset ExpiresAtUtc,
    bool IsLocal)
{
    public bool NeedsRefresh(DateTimeOffset nowUtc) =>
        !IsLocal && ExpiresAtUtc <= nowUtc.AddMinutes(2);
}
