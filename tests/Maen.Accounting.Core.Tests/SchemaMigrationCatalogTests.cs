using Maen.Accounting.Core.Services;

namespace Maen.Accounting.Core.Tests;

public sealed class SchemaMigrationCatalogTests
{
    [Fact]
    public void Migrations_AreStrictlyIncreasingAndUnique()
    {
        var migrations = SchemaMigrationCatalog.All;

        Assert.NotEmpty(migrations);
        Assert.Equal(migrations.Count, migrations.Select(static item => item.Version).Distinct().Count());
        Assert.Equal(
            migrations.Select(static item => item.Version).OrderBy(static version => version),
            migrations.Select(static item => item.Version));
        Assert.All(migrations, migration => Assert.False(string.IsNullOrWhiteSpace(migration.Name)));
    }

    [Fact]
    public void CurrentVersion_IsLatestCatalogVersion()
    {
        Assert.Equal(SchemaMigrationCatalog.All[^1].Version, SchemaMigrationCatalog.CurrentVersion);
    }

    [Fact]
    public void BusinessEntitySyncIndexes_ArePartOfCatalog()
    {
        var migration = Assert.Single(
            SchemaMigrationCatalog.All,
            item => item.Name == "business-entity-sync-indexes");

        Assert.Equal(4, migration.Version);
    }
}
