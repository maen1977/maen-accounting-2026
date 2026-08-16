namespace Maen.Accounting.Core.Services;

public sealed record SchemaMigrationDefinition(int Version, string Name);

public static class SchemaMigrationCatalog
{
    public static IReadOnlyList<SchemaMigrationDefinition> All { get; } =
    [
        new(1, "personal-movement-columns"),
        new(2, "business-payment-account-code"),
        new(3, "ledger-and-query-indexes"),
        new(4, "business-entity-sync-indexes"),
        new(5, "personal-plans-obligations-deposits"),
        new(6, "business-payment-soft-delete"),
        new(7, "document-integrity-hashes"),
        new(8, "personal-savings-goals"),
        new(9, "entry-attachments-and-recurring-movements"),
        new(10, "multi-currency-and-budget-plans")
    ];

    public static int CurrentVersion => All[^1].Version;
}
