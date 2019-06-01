using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Text;
using Shoko.Core.Addon;
using Shoko.Core.Models;

namespace Shoko.Core.Database
{
    /// <summary>
    /// This is marked as abstract to stop instansiation that shouldn't occur.
    /// </summary>
    public abstract class PluginDbContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder
                .UseLazyLoadingProxies();
            //TODO: Replace with a switch for database types.
            optionsBuilder.UseMySql(Config.ConfigurationLoader.CoreConfig.ConnectionString); //temp connection string
            
            /*
            switch (_type)
            {
                case DatabaseTypes.SqlServer:
                    optionsBuilder.UseSqlServer(_connectionString);
                    break;
                case DatabaseTypes.MySql:
                    optionsBuilder.UseMySql(_connectionString);
                    break;
                case DatabaseTypes.Sqlite:
                    optionsBuilder.UseSqlite(_connectionString);
                    break;
            }*/ 
        }

        /// <summary>
        /// This is required to be ran as part of the PluginDbContext to prevent table collisions.
        /// </summary>
        /// <param name="modelBuilder"></param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            foreach (IMutableEntityType entity in modelBuilder.Model.GetEntityTypes())
            {
                entity.Relational().TableName = AddonRegistry.AssemblyToPluginMap[this.GetType().Assembly] + "_" + entity.Relational().TableName;
            }
        }
    }
}
