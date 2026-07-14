namespace InvestView.Infrastructure.Data;

public sealed class DatabaseMigrationOptions
{
    public const string SectionName = "Database";

    public bool ApplyMigrationsOnStartup { get; set; } = true;
}
