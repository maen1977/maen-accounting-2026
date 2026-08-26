using Maen.Accounting.Core.Models;
using SQLite;

namespace Maen.Accounting.App.Data;

[Table("company_profiles")]
public sealed class CompanyProfileRow
{
    [PrimaryKey]
    public string UserId { get; set; } = string.Empty;

    public string CompanyName { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public string TaxNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public long CapitalMinor { get; set; }
    public string CurrencyCode { get; set; } = CompanyProfileDefaults.CurrencyCode;
    public long UpdatedAtUtcTicks { get; set; }
    public int Version { get; set; } = 1;
    public string DeviceId { get; set; } = string.Empty;

    public static CompanyProfileRow FromModel(CompanyProfile profile) => new()
    {
        UserId = profile.UserId,
        CompanyName = profile.CompanyName ?? string.Empty,
        LegalName = profile.LegalName ?? string.Empty,
        RegistrationNumber = profile.RegistrationNumber ?? string.Empty,
        TaxNumber = profile.TaxNumber ?? string.Empty,
        Address = profile.Address ?? string.Empty,
        Phone = profile.Phone ?? string.Empty,
        Email = profile.Email ?? string.Empty,
        CapitalMinor = profile.CapitalMinor,
        CurrencyCode = string.IsNullOrWhiteSpace(profile.CurrencyCode)
            ? CompanyProfileDefaults.CurrencyCode
            : profile.CurrencyCode,
        UpdatedAtUtcTicks = (profile.UpdatedAtUtc ?? DateTimeOffset.UtcNow).UtcDateTime.Ticks,
        Version = profile.Version,
        DeviceId = profile.DeviceId ?? string.Empty
    };

    public CompanyProfile ToModel() => new(
        UserId ?? string.Empty,
        CompanyName ?? string.Empty,
        LegalName ?? string.Empty,
        RegistrationNumber ?? string.Empty,
        TaxNumber ?? string.Empty,
        Address ?? string.Empty,
        Phone ?? string.Empty,
        Email ?? string.Empty,
        CapitalMinor,
        string.IsNullOrWhiteSpace(CurrencyCode) ? CompanyProfileDefaults.CurrencyCode : CurrencyCode,
        new DateTimeOffset(UpdatedAtUtcTicks, TimeSpan.Zero),
        Version,
        DeviceId ?? string.Empty);
}
