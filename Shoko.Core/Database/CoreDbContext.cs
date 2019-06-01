using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Shoko.Core.Models;

namespace Shoko.Core.Database
{
    internal class CoreDbContext : PluginDbContext
    {
        public DbSet<ShokoUser> Users { get; set; }

        public DbSet<DataFolder> DataFolders { get;set; }
        public DbSet<VideoFile> VideoFiles { get;set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            //This is being overridden so that we don't call the base.
            //Since the base forces a table prefix 

            

            modelBuilder.Entity<DataFolder>()
                .Property(e => e.Path)
                .HasConversion(new ValueConverter<Uri, string>(
                    v => v.ToString(),
                    v => new Uri(v, UriKind.Absolute)
                ));

            modelBuilder.Entity<VideoFile>()
                .Property(e => e.Path)
                .HasConversion(new ValueConverter<Uri, string>(
                    v => v.ToString(),
                    v => new Uri(v, UriKind.Relative)
                ));
        }
    }
}
