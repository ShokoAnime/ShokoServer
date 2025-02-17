using Microsoft.EntityFrameworkCore.Design;

namespace Shoko.Server.Data.Context;

public class DesignTimeContext : IDesignTimeDbContextFactory<DataContext>
{
    public DataContext CreateDbContext(string[] args)
    {
        return new DataContext();
    }
}
