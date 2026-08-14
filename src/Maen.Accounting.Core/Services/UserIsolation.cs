using System.Security.Cryptography;
using System.Text;

namespace Maen.Accounting.Core.Services;

public static class UserIsolation
{
    public static string NormalizeEmail(string email) =>
        string.IsNullOrWhiteSpace(email)
            ? throw new ArgumentException("Email is required.", nameof(email))
            : email.Trim().ToLowerInvariant();

    public static string SafeHash(string value, int length = 24)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant()[..length];
    }

    public static string DatabaseFileName(string userId) =>
        $"maen_{SafeHash(userId)}.db3";

    public static string LocalUserId(string email) =>
        $"local_{SafeHash(NormalizeEmail(email), 32)}";

    public static string LocalDeviceUserId(string deviceId) =>
        $"local_device_{SafeHash(deviceId, 32)}";

    public static void EnsureOwner(string expectedUserId, string actualUserId)
    {
        if (!string.Equals(expectedUserId, actualUserId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The financial record belongs to a different user.");
        }
    }
}
