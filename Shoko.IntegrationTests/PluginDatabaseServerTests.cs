using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Shoko.Abstractions.Plugin;
using Shoko.IntegrationTests.PluginDatabases;
using Shoko.Server.Plugin;
using Shoko.Server.Plugin.Databases;
using Shoko.Server.Server;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Runs a plugin database with migrations for SQLite, MySQL and SQL Server against the backend
/// this run's server is on: in its own file on SQLite, and otherwise in the core's database with
/// its tables prefixed. Covers the migrations, the context, the copy taken before migrating, and
/// dropping the tables when the plugin is uninstalled with its data.
/// </summary>
/// <remarks>
/// Each test gives the sample plugin an ID of its own, so its tables never meet another test's,
/// and drops them when it is done.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class PluginDatabaseServerTests(DatabaseMigrationFixture fixture) : IDisposable
{
    private const string Name = "sample";

    private readonly List<(Guid PluginID, ServiceProvider Provider)> _plugins = [];

    private PluginDatabaseServer Server => fixture.Services.GetRequiredService<PluginDatabaseServer>();

    private Constants.DatabaseType Backend => fixture.Backend switch
    {
        "MySQL" or "MariaDB" => Constants.DatabaseType.MySQL,
        "SQLServer" or "MSSQL" => Constants.DatabaseType.SQLServer,
        _ => Constants.DatabaseType.SQLite,
    };

    public void Dispose()
    {
        foreach (var (pluginID, provider) in _plugins)
        {
            provider.Dispose();
            if (Backend is not Constants.DatabaseType.SQLite)
                PluginDatabaseServer.DropTables(Backend, Server.ConnectionString, PluginTableNaming.GetPluginPrefix(pluginID));
        }

        SqliteConnection.ClearAllPools();
    }

    #region Placement

    [Fact]
    public void TheSampleIsKeptOnTheCoresBackend()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var (provider, database) = Register();

        provider.GetRequiredService<PluginDatabaseMigrator>().MigrateAll();

        Assert.Equal(Backend, database.Target.Type);
        Assert.Equal(
            Backend switch
            {
                Constants.DatabaseType.MySQL => typeof(MySqlSampleNotesContext),
                Constants.DatabaseType.SQLServer => typeof(SqlServerSampleNotesContext),
                _ => typeof(SampleNotesContext),
            },
            database.Target.ContextType
        );
        var history = ReadHistory(database);
        Assert.Equal(2, history.Count);
        Assert.EndsWith("_CreateNotes", history[0]);
        Assert.EndsWith("_AddTag", history[1]);

        if (Backend is Constants.DatabaseType.SQLite)
        {
            Assert.True(File.Exists(database.FilePath));
            Assert.Null(database.Target.Naming);
            return;
        }

        // Every table and the foreign key carry the prefix, the history included, and nothing
        // went into the core's database under the names the plugin gave.
        var prefix = database.Target.Naming!.Prefix;
        Assert.Equal(
            new[] { "Books", "Notes", PluginTableNaming.HistoryTable }.Select(table => prefix + table).Order(StringComparer.Ordinal),
            Server.GetTables(prefix).Select(TableName).Order(StringComparer.Ordinal)
        );
        Assert.DoesNotContain(Server.GetTables(string.Empty).Select(TableName), table => table is "Books" or "Notes");
        Assert.All(ReadForeignKeys(prefix + "Notes"), name => Assert.StartsWith(prefix, name));
        Assert.Single(ReadForeignKeys(prefix + "Notes"));
        Assert.False(File.Exists(database.FilePath), $"{database.FilePath} was created while the database is on the server.");
    }

    [Fact]
    public void ThePluginReachesTheSampleThroughTheContextItRegistered()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var (provider, _) = Register();
        provider.GetRequiredService<PluginDatabaseMigrator>().MigrateAll();

        var factory = provider.GetRequiredService<IDbContextFactory<SampleNotesContext>>();
        using (var context = factory.CreateDbContext())
        {
            // The seed data went into the renamed table too.
            var book = context.Books.Single();
            Assert.Equal(SampleNotesContext.SeededBook, book.Title);
            context.Notes.Add(new SampleNote { Text = "written through the context", Tag = "tagged", BookID = book.ID });
            context.SaveChanges();
        }

        using var scope = provider.CreateScope();
        var note = scope.ServiceProvider.GetRequiredService<SampleNotesContext>().Notes.Include(row => row.Book).Single();
        Assert.Equal("written through the context", note.Text);
        Assert.Equal("tagged", note.Tag);
        Assert.Equal(SampleNotesContext.SeededBook, note.Book!.Title);
    }

    [Fact]
    public void APluginWithOnlySqliteMigrationsKeepsItsFile()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        Assert.SkipWhen(Backend is Constants.DatabaseType.SQLite, "Every plugin database is a SQLite file while the core runs on SQLite.");

        var database = fixture.Services.GetServices<IPluginDatabase>().Single(database => database.PluginID == TestPlugins.DatabasePluginID);

        Assert.Equal(Constants.DatabaseType.SQLite, database.Target.Type);
        Assert.True(File.Exists(database.FilePath));
    }

    #endregion

    #region Backups

    [Fact]
    public void ADatabaseWithTablesIsCopiedBeforeItsNextMigration()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var (provider, database) = Register();
        var migrations = database.GetPendingMigrations();
        database.Migrate(migrations[0]);

        var backupDirectory = Path.Join(Path.GetTempPath(), $"shoko-plugin-database-backups-{Guid.NewGuid():N}");
        try
        {
            var migrator = new PluginDatabaseMigrator(
                provider.GetServices<IPluginDatabase>(),
                Server,
                provider.GetRequiredService<IApplicationPaths>(),
                provider.GetRequiredService<PluginDatabaseGate>(),
                provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PluginDatabaseMigrator>>()
            )
            { BackupDirectory = backupDirectory };
            migrator.MigrateAll();

            Assert.Equal(2, ReadHistory(database).Count);
            var folder = Path.Join(backupDirectory, database.PluginID.ToString());
            switch (Backend)
            {
                case Constants.DatabaseType.SQLServer:
                    // Each migration runs in a transaction, and the core's backups hold the tables.
                    Assert.False(Directory.Exists(folder));
                    break;
                case Constants.DatabaseType.MySQL:
                    var dump = File.ReadAllText(Assert.Single(Directory.GetFiles(folder, $"{Name}_*.sql")));
                    Assert.Contains(database.Target.Naming!.Prefix + "Books", dump);
                    Assert.Contains(SampleNotesContext.SeededBook, dump);
                    Assert.DoesNotContain("`AniDB_Anime`", dump);
                    break;
                default:
                    Assert.Single(Directory.GetFiles(folder, $"{Name}_*.db3"));
                    break;
            }
        }
        finally
        {
            if (Directory.Exists(backupDirectory))
                Directory.Delete(backupDirectory, recursive: true);
        }
    }

    #endregion

    #region Uninstalling

    [Fact]
    public void UninstallingThePluginWithItsDataDropsItsTables()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var (provider, database) = Register();
        provider.GetRequiredService<PluginDatabaseMigrator>().MigrateAll();
        var folder = Path.GetDirectoryName(database.FilePath)!;
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Join(folder, ".remove"), string.Empty);

        ((PluginManager)fixture.Services.GetRequiredService<IPluginManager>()).RemovePluginDataMarkedForRemoval();

        Assert.False(Directory.Exists(folder));
        if (Backend is not Constants.DatabaseType.SQLite)
            Assert.Empty(Server.GetTables(PluginTableNaming.GetPluginPrefix(database.PluginID)));
    }

    #endregion

    #region Helpers

    private (ServiceProvider Provider, IPluginDatabase Database) Register()
    {
        var pluginID = Guid.NewGuid();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(fixture.Services.GetRequiredService<IApplicationPaths>());
        services.AddSingleton(SamplePluginManager.Create(fixture.Services.GetRequiredService<IPluginManager>(), pluginID));
        services.AddSingleton(typeof(PluginPaths<>));
        services.AddSingleton(Server);
        services.AddSingleton<PluginDatabaseMigrator>();
        services.AddPluginDbContext<SamplePlugin, SampleNotesContext>(Name, options => options
            .WithMySqlMigrations<MySqlSampleNotesContext>()
            .WithSqlServerMigrations<SqlServerSampleNotesContext>()
        );
        PluginDatabaseRegistrar.AddPluginDatabases(services);
        var provider = services.BuildServiceProvider();
        _plugins.Add((pluginID, provider));
        return (provider, provider.GetServices<IPluginDatabase>().Single());
    }

    private static string TableName(string table)
        => table[(table.IndexOf('.') + 1)..];

    private List<string> ReadHistory(IPluginDatabase database)
        => database.Target.Naming is { } naming
            ? Query(Server.ConnectionString, $"SELECT MigrationId FROM {Quote(naming.HistoryTableName)} ORDER BY MigrationId")
            : Query(null, "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId", database.FilePath);

    private List<string> ReadForeignKeys(string table)
        => Backend is Constants.DatabaseType.MySQL
            ? Query(Server.ConnectionString, $"SELECT CONSTRAINT_NAME FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE() AND CONSTRAINT_TYPE = 'FOREIGN KEY' AND TABLE_NAME = '{table}'")
            : Query(Server.ConnectionString, $"SELECT name FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('{table}')");

    private string Quote(string identifier)
        => Backend is Constants.DatabaseType.MySQL ? $"`{identifier}`" : $"[{identifier}]";

    private List<string> Query(string? connectionString, string sql, string? sqliteFile = null)
    {
        using DbConnection connection = Backend switch
        {
            _ when sqliteFile is not null => new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = sqliteFile, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()),
            Constants.DatabaseType.MySQL => new MySqlConnection(connectionString),
            _ => new SqlConnection(connectionString),
        };
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var values = new List<string>();
        while (reader.Read())
            values.Add(reader.GetString(0));
        return values;
    }

    #endregion
}
