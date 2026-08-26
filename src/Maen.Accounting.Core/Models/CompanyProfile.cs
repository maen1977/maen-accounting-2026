namespace Maen.Accounting.Core.Models;

public sealed record CompanyProfile(
    string UserId,
    string CompanyName,
    string LegalName = "",
    string RegistrationNumber = "",
    string TaxNumber = "",
    string Address = "",
    string Phone = "",
    string Email = "",
    long CapitalMinor = 0,
    string CurrencyCode = "JOD",
    DateTimeOffset? UpdatedAtUtc = null,
    int Version = 1,
    string DeviceId = "")
{
    public string DisplayName => string.IsNullOrWhiteSpace(LegalName) ? CompanyName : LegalName;
}

public static class CompanyProfileDefaults
{
    public const string CurrencyCode = "JOD";
    public const long CapitalMinor = 0;
}
