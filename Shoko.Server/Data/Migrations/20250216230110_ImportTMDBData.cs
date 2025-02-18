using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Commons.Extensions;
using Shoko.Server.Data.Context;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Utilities;

#nullable disable

namespace Shoko.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class ImportTMDBData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // We need to:
            // set up connections to the old database, theoretically, we could do this with EF
            // read data
            // enable identity insert on the new database
            // save it in the new database
            // disable identity insert on the new database
            // do it all in batches

            // TODO find a way to set the IDs to 0 without breaking relationships
            // Probably start with TMDB_Show, and build on it.
            // We have TMDB IDs, so we can look up shared ones like Person, Company, Season, etc

            /*var settings = Utils.SettingsProvider.GetSettings();
            var databaseType = settings.Database.Type;
            var batchSize = 1000;
            using var oldContext = new DataContext { DatabaseType = databaseType, ConnectionString = $@"data source={SQLite.GetDatabaseFilePath()};" };

            // we create a scope and Context each batch to save memory and keep the change tracker clean
            foreach (var batch in oldContext.Set<TMDB_AlternateOrdering>().AsNoTracking().Batch(batchSize))
            {
                using var scope = Utils.ServiceContainer.CreateScope();
                using var newContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                newContext.Set<TMDB_AlternateOrdering>().AddRange(batch);
                newContext.SaveChanges();
            }

            foreach (var batch in oldContext.Set<TMDB_AlternateOrdering_Episode>().AsNoTracking().Batch(batchSize))
            {
                using var scope = Utils.ServiceContainer.CreateScope();
                using var newContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                newContext.Set<TMDB_AlternateOrdering_Episode>().AddRange(batch);
                newContext.SaveChanges();
            }
            
            foreach (var batch in oldContext.Set<TMDB_AlternateOrdering_Season>().AsNoTracking().Batch(batchSize))
            {
                using var scope = Utils.ServiceContainer.CreateScope();
                using var newContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                newContext.Set<TMDB_AlternateOrdering_Season>().AddRange(batch);
                newContext.SaveChanges();
            }
            
            foreach (var batch in oldContext.Set<TMDB_Collection>().AsNoTracking().Batch(batchSize))
            {
                using var scope = Utils.ServiceContainer.CreateScope();
                using var newContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                newContext.Set<TMDB_Collection>().AddRange(batch);
                newContext.SaveChanges();
            }
            
            foreach (var batch in oldContext.Set<TMDB_Collection_Movie>().AsNoTracking().Batch(batchSize))
            {
                using var scope = Utils.ServiceContainer.CreateScope();
                using var newContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                newContext.Set<TMDB_Collection_Movie>().AddRange(batch);
                newContext.SaveChanges();
            }
            
            foreach (var batch in oldContext.Set<TMDB_Network>().AsNoTracking().Batch(batchSize))
            {
                using var scope = Utils.ServiceContainer.CreateScope();
                using var newContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                newContext.Set<TMDB_Network>().AddRange(batch);
                newContext.SaveChanges();
            }
            
            foreach (var batch in oldContext.Set<TMDB_Show_Network>().AsNoTracking().Batch(batchSize))
            {
                using var scope = Utils.ServiceContainer.CreateScope();
                using var newContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                newContext.Set<TMDB_Show_Network>().AddRange(batch);
                newContext.SaveChanges();
            }
            
            foreach (var batch in oldContext.Set<TMDB_Company>().AsNoTracking().Batch(batchSize))
            {
                using var scope = Utils.ServiceContainer.CreateScope();
                using var newContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                newContext.Set<TMDB_Company>().AddRange(batch);
                newContext.SaveChanges();
            }
            
            foreach (var batch in oldContext.Set<TMDB_Company_Entity>().AsNoTracking().Batch(batchSize))
            {
                using var scope = Utils.ServiceContainer.CreateScope();
                using var newContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                newContext.Set<TMDB_Company_Entity>().AddRange(batch);
                newContext.SaveChanges();
            }

            foreach (var batch in oldContext.Set<TMDB_Episode_Cast>().AsNoTracking().Batch(batchSize))
            {
                using var scope = Utils.ServiceContainer.CreateScope();
                using var newContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                newContext.Set<TMDB_Episode_Cast>().AddRange(batch);
                newContext.SaveChanges();
            }
            
            foreach (var batch in oldContext.Set<TMDB_Episode_Crew>().AsNoTracking().Batch(batchSize))
            {
                using var scope = Utils.ServiceContainer.CreateScope();
                using var newContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                newContext.Set<TMDB_Episode_Crew>().AddRange(batch);
                newContext.SaveChanges();
            }
            
            foreach (var batch in oldContext.Set<TMDB_Episode>().AsNoTracking().Batch(batchSize))
            {
                using var scope = Utils.ServiceContainer.CreateScope();
                using var newContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                newContext.Set<TMDB_Episode>().AddRange(batch);
                newContext.SaveChanges();
            }
            
            foreach (var batch in oldContext.Set<TMDB_Image>().AsNoTracking().Batch(batchSize))
            {
                using var scope = Utils.ServiceContainer.CreateScope();
                using var newContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                newContext.Set<TMDB_Image>().AddRange(batch);
                newContext.SaveChanges();
            }
            
            foreach (var batch in oldContext.Set<TMDB_Image_Entity>().AsNoTracking().Batch(batchSize))
            {
                using var scope = Utils.ServiceContainer.CreateScope();
                using var newContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                newContext.Set<TMDB_Image_Entity>().AddRange(batch);
                newContext.SaveChanges();
            }
            
            foreach (var batch in oldContext.Set<TMDB_Movie_Cast>().AsNoTracking().Batch(batchSize))
            {
                using var scope = Utils.ServiceContainer.CreateScope();
                using var newContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                newContext.Set<TMDB_Movie_Cast>().AddRange(batch);
                newContext.SaveChanges();
            }

            foreach (var batch in oldContext.Set<TMDB_Movie_Crew>().AsNoTracking().Batch(batchSize))
            {
                using var scope = Utils.ServiceContainer.CreateScope();
                using var newContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                newContext.Set<TMDB_Movie_Crew>().AddRange(batch);
                newContext.SaveChanges();
            }

            foreach (var batch in oldContext.Set<TMDB_Movie>().AsNoTracking().Batch(batchSize))
            {
                using var scope = Utils.ServiceContainer.CreateScope();
                using var newContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                newContext.Set<TMDB_Movie>().AddRange(batch);
                newContext.SaveChanges();
            }
            
            foreach (var batch in oldContext.Set<TMDB_Person>().AsNoTracking().Batch(batchSize))
            {
                using var scope = Utils.ServiceContainer.CreateScope();
                using var newContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                newContext.Set<TMDB_Person>().AddRange(batch);
                newContext.SaveChanges();
            }
            
            foreach (var batch in oldContext.Set<TMDB_Season>().AsNoTracking().Batch(batchSize))
            {
                using var scope = Utils.ServiceContainer.CreateScope();
                using var newContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                newContext.Set<TMDB_Season>().AddRange(batch);
                newContext.SaveChanges();
            }
            
            foreach (var batch in oldContext.Set<TMDB_Show>().AsNoTracking().Batch(batchSize))
            {
                using var scope = Utils.ServiceContainer.CreateScope();
                using var newContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                newContext.Set<TMDB_Show>().AddRange(batch);
                newContext.SaveChanges();
            }
            
            foreach (var batch in oldContext.Set<TMDB_Overview>().AsNoTracking().Batch(batchSize))
            {
                using var scope = Utils.ServiceContainer.CreateScope();
                using var newContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                newContext.Set<TMDB_Overview>().AddRange(batch);
                newContext.SaveChanges();
            }
            
            foreach (var batch in oldContext.Set<TMDB_Title>().AsNoTracking().Batch(batchSize))
            {
                using var scope = Utils.ServiceContainer.CreateScope();
                using var newContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                newContext.Set<TMDB_Title>().AddRange(batch);
                newContext.SaveChanges();
            }

            throw new Exception("Test");*/
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
