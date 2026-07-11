using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InvestView.Infrastructure.Data;

public sealed class InvestViewDbContextFactory : IDesignTimeDbContextFactory<InvestViewDbContext>
{
    public const string DefaultConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=InvestView;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

    public InvestViewDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("INVESTVIEW_DB_CONNECTION_STRING")
            ?? DefaultConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<InvestViewDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new InvestViewDbContext(optionsBuilder.Options);
    }
}
