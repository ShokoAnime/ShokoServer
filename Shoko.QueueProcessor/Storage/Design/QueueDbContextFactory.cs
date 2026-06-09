using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Shoko.QueueProcessor.Storage.Contexts;

[assembly: DesignTimeServicesReference("Shoko.QueueProcessor.Storage.Design.QueueDesignTimeServices, Shoko.QueueProcessor")]

namespace Shoko.QueueProcessor.Storage.Design;

/// <summary>
/// Design-time factory used by <c>dotnet ef migrations add</c>.
/// Uses SQLite as the baseline provider; EF Core translates the same C# migration
/// operations to provider-appropriate SQL at runtime for MySQL and SQL Server.
/// </summary>
internal class QueueDbContextFactory : IDesignTimeDbContextFactory<SqliteQueueDbContext>
{
    public SqliteQueueDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SqliteQueueDbContext>()
            .UseSqlite("Data Source=queue-design-time.db")
            .Options;
        return new SqliteQueueDbContext("Data Source=queue-design-time.db");
    }
}
