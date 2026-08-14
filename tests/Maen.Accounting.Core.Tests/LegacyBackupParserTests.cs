using Maen.Accounting.Core.Services;

namespace Maen.Accounting.Core.Tests;

public sealed class LegacyBackupParserTests
{
    [Fact]
    public void Imports_flutter_version_two_backup_into_minor_units()
    {
        const string json = """
        {
          "version": 2,
          "backupEmail": "person@example.com",
          "entries": [
            {"entry_date":"2026-07-01","sales":12.34,"cost":2.10,"expenses":0.24,"notes":"اختبار"}
          ]
        }
        """;

        var result = LegacyBackupParser.Parse(
            json, "uid-a", "person@example.com", "device", DateTimeOffset.UtcNow);
        var entry = Assert.Single(result.Entries);
        Assert.Equal(1234, entry.SalesMinor);
        Assert.Equal(210, entry.CostMinor);
        Assert.Equal(24, entry.ExpensesMinor);
        Assert.Equal("uid-a", entry.UserId);
    }

    [Fact]
    public void Rejects_backup_from_another_email()
    {
        const string json = """{"version":2,"backupEmail":"other@example.com","entries":[]}""";
        Assert.Throws<InvalidOperationException>(() =>
            LegacyBackupParser.Parse(json, "uid-a", "person@example.com", "device", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Accepts_personal_backup_only_for_personal_scope()
    {
        const string json = """{"version":4,"accountScope":"personal","backupEmail":"person@example.com","entries":[]}""";

        var result = LegacyBackupParser.Parse(
            json, "uid-a", "person@example.com", "device", DateTimeOffset.UtcNow, "personal");

        Assert.Equal(4, result.Version);
    }

    [Fact]
    public void Rejects_personal_backup_in_business_scope()
    {
        const string json = """{"version":4,"accountScope":"personal","backupEmail":"person@example.com","entries":[]}""";

        Assert.Throws<InvalidOperationException>(() =>
            LegacyBackupParser.Parse(
                json, "uid-a", "person@example.com", "device", DateTimeOffset.UtcNow, "business"));
    }
}
