using SQLite;

namespace Maen.Accounting.App.Data;

[Table("schema_migrations")]
public sealed class SchemaMigrationRow
{
    [PrimaryKey]
    public int Version { get; set; }

    [Indexed]
    public string Name { get; set; } = string.Empty;

    public long AppliedAtUtcTicks { get; set; }
}
