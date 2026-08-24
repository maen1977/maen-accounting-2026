using System.Security.Cryptography;
using System.Text;
using Microsoft.Maui.Storage;

namespace Maen.Accounting.App.Services;

/// <summary>
/// Optional app-level lock: a user-defined PIN whose salted SHA-256 hash is stored
/// in the platform secure store. The lock activates after a configured idle window
/// and blocks the whole application until the PIN is entered correctly.
/// </summary>
public static class AppLockService
{
    public const int MinimumPinLength = 4;
    public const int MaximumPinLength = 8;
    public const int DefaultIdleMinutes = 5;

    private const string PinHashKey = "maen.app.pin.hash.v1";
    private const string SaltKey = "maen.app.pin.salt.v1";
    private const string EnabledKey = "maen.app.lock.enabled.v1";
    private const string IdleKey = "maen.app.lock.idle.minutes.v1";

    public static async Task<bool> IsEnabledAsync()
    {
        return await ReadBoolAsync(EnabledKey);
    }

    public static async Task SetEnabledAsync(bool enabled)
    {
        await SecureStorage.Default.SetAsync(EnabledKey, enabled ? "1" : "0");
    }

    public static async Task<int> GetIdleMinutesAsync()
    {
        var raw = await SecureStorage.Default.GetAsync(IdleKey);
        return int.TryParse(raw, out var minutes) && minutes >= 1 ? minutes : DefaultIdleMinutes;
    }

    public static async Task SetIdleMinutesAsync(int minutes)
    {
        var clamped = Math.Clamp(minutes, 1, 60);
        await SecureStorage.Default.SetAsync(IdleKey, clamped.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>Creates or replaces the lock PIN. Returns true when the PIN is acceptable and stored.</summary>
    public static async Task<bool> SetPinAsync(string pin)
    {
        if (!IsValidPin(pin))
        {
            return false;
        }

        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = HashPin(pin, salt);

        await SecureStorage.Default.SetAsync(PinHashKey, hash);
        await SecureStorage.Default.SetAsync(SaltKey, Convert.ToBase64String(salt));
        await SecureStorage.Default.SetAsync(EnabledKey, "1");
        return true;
    }

    public static async Task<bool> HasPinAsync()
    {
        var stored = await SecureStorage.Default.GetAsync(PinHashKey);
        return !string.IsNullOrEmpty(stored);
    }

    public static async Task<bool> VerifyPinAsync(string pin)
    {
        var storedHash = await SecureStorage.Default.GetAsync(PinHashKey);
        var storedSalt = await SecureStorage.Default.GetAsync(SaltKey);
        if (string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(storedSalt))
        {
            return false;
        }

        byte[] salt;
        try
        {
            salt = Convert.FromBase64String(storedSalt);
        }
        catch (FormatException)
        {
            return false;
        }

        return string.Equals(HashPin(pin, salt), storedHash, StringComparison.Ordinal);
    }

    public static bool IsValidPin(string? pin) =>
        pin is { Length: >= MinimumPinLength and <= MaximumPinLength }
        && pin.All(static character => character is >= '0' and <= '9');

    private static string HashPin(string pin, byte[] salt)
    {
        var payload = new byte[salt.Length + Encoding.UTF8.GetByteCount(pin)];
        salt.CopyTo(payload, 0);
        Encoding.UTF8.GetBytes(pin, 0, pin.Length, payload, salt.Length);
        var digest = SHA256.HashData(payload);
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    private static async Task<bool> ReadBoolAsync(string key)
    {
        var raw = await SecureStorage.Default.GetAsync(key);
        return string.Equals(raw, "1", StringComparison.Ordinal);
    }
}
