using System.Security.Cryptography;
using System.Text;

namespace Maen.Accounting.Core.Services;

/// <summary>
/// Produces deterministic integrity hashes for financial documents so that
/// tampered uploads or corrupted local stores can be detected. The hash
/// binds the entity identity, version, and monetary fields together.
/// </summary>
public static class DataIntegrityService
{
    public const string IntegrityHashAlgorithm = "sha256-v1";

    public static string ComputeInvoiceHash(
        string invoiceId,
        string userId,
        int version,
        int type,
        long totalMinor,
        long taxMinor,
        int status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return Hash($"{IntegrityHashAlgorithm}|{invoiceId}|{userId}|{version}|{type}|{totalMinor}|{taxMinor}|{status}");
    }

    public static string ComputePaymentHash(
        string paymentId,
        string userId,
        int version,
        int type,
        long amountMinor,
        bool isDeleted)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return Hash($"{IntegrityHashAlgorithm}|{paymentId}|{userId}|{version}|{type}|{amountMinor}|{isDeleted}");
    }

    public static string ComputeProfitEntryHash(
        string entryId,
        string userId,
        int version,
        long salesMinor,
        long costMinor,
        long expensesMinor,
        bool isDeleted)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return Hash($"{IntegrityHashAlgorithm}|{entryId}|{userId}|{version}|{salesMinor}|{costMinor}|{expensesMinor}|{isDeleted}");
    }

    public static bool Verify(string expected, string actual) =>
        !string.IsNullOrWhiteSpace(expected)
        && !string.IsNullOrWhiteSpace(actual)
        && string.Equals(expected.Trim(), actual.Trim(), StringComparison.Ordinal);

    private static string Hash(string payload)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
