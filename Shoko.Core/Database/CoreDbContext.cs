using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Shoko.Core.Database
{
    internal class CoreDbContext : PluginDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            //This is being overridden so that we don't call the base.
            //Since the base forces a table prefix 
        }
    }
}
