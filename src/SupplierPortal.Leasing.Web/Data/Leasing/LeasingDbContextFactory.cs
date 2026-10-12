using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SupplierPortal.Web.Data.Leasing;

/// <summary>
/// Design-time factory so <c>dotnet ef</c> can build <see cref="LeasingDbContext"/> for migrations without
/// booting the full web host (which would run startup seeding and require a live database). Reads the same
/// connection string layering as the app.
/// </summary>
public class LeasingDbContextFactory : IDesignTimeDbContextFactory<LeasingDbContext>
{
    public LeasingDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .Build();

        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? "Server=localhost;Database=BP;Trusted_Connection=True;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<LeasingDbContext>()
            .UseSqlServer(connectionString,
                sql => sql.MigrationsHistoryTable("__EFLeasingMigrationsHistory", LeasingDbContext.Schema))
            .Options;

        return new LeasingDbContext(options);
    }
}
