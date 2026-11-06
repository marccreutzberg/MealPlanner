using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MealPlanner.Api.Common.Data;

/// <summary>
/// Used by dotnet-ef only. The connection string is never opened when adding migrations.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Data Source=np:localhost;Initial Catalog=MealPlanner;Integrated Security=true;TrustServerCertificate=true")
            .Options;
        return new AppDbContext(options);
    }
}
