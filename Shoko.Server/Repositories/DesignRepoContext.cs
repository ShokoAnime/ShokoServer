using Microsoft.EntityFrameworkCore.Design;
using System;
using System.Collections.Generic;
using System.Text;

namespace Shoko.Server.Repositories
{
    public class DesignRepoContext : IDesignTimeDbContextFactory<ShokoContext>
    {
        public ShokoContext CreateDbContext(string[] args)
        {
            return new ShokoContext(DatabaseTypes.Sqlite, "Data Source=shoko.db");
        }
    }
}
