using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Core.Exceptions;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Server.Plugin;
using Shoko.Server.Plugin.Databases;
using Shoko.Server.Server;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Shoko.Tests.Metadata;
using Xunit;

namespace Shoko.Tests.Plugin;

/// <summary>
/// Covers the plugin databases: how a registration becomes a context, the plugins starting without
/// them, the gate that keeps them closed until the late start has migrated them, the copies taken
/// before migrating and next to the core's backups, and the start-up failure a migration that
/// fails leaves.
/// </summary>
/// <remarks>
/// Starting the plugins closes registration of metadata sources, which is process-global, so these
/// run with the other tests that close it and open it again before they finish.
/// </remarks>
[Collection(nameof(MetadataRegistrationCollection))]
public sealed class PluginDatabaseTests : IDisposable
{
    private readonly string _root = Path.Join(Path.GetTempPath(), $"shoko-plugin-database-{Guid.NewGuid():N}");

    public PluginDatabaseTests()
        => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    #region Start-Up Order

    [Fact]
    public void PluginsAreSetUpThenMadeReadyWithoutTheirDatabasesBeingMigrated()
    {
        var log = new List<string>();
        var manager = CreateManager(new RecordingPlugin(log, "first"), new RecordingPlugin(log, "second"));
        var database = new FakeDatabase(this, ["20260101000000_One", "20260102000000_Two"]) { Log = log };
        using var services = Services(database);
        try
        {
            manager.StartPlugins(services, _ => { });
        }
        finally
        {
            Unfreeze();
        }

        Assert.Equal(["setup first", "setup second", "ready first", "ready second"], log);
        Assert.Empty(database.Applied);
        Assert.False(services.GetRequiredService<PluginDatabaseGate>().IsOpen);
    }

    #endregion

    #region Gate

    [Fact]
    public void APluginCannotReachItsDatabaseBeforeItIsMigrated()
    {
        using var provider = RegisterNotes(out var pluginInfo);

        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IDbContextFactory<NotesContext>>().CreateDbContext());
        using (var scope = provider.CreateScope())
            Assert.Throws<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<NotesContext>());

        Assert.False(File.Exists(Path.Join(_root, "data", pluginInfo.ID.ToString(), "notes.db3")));
    }

    [Fact]
    public void AfterAFailedStartUpTheDatabaseSaysItWasNeverMigrated()
    {
        var failure = new StartupFailedException("Could not connect to database!");
        var gate = new PluginDatabaseGate(Mock.Of<ISystemService>(service => service.StartupFailedException == failure));

        var ex = Assert.Throws<InvalidOperationException>(() => gate.ThrowIfClosed("Fake Plugin", "notes"));

        Assert.Contains("the server failed to start before migrating it", ex.Message);
        Assert.Same(failure, ex.InnerException);
    }

    [Fact]
    public void TheDatabasesOpenOnceEveryOneIsMigrated()
    {
        var gate = new PluginDatabaseGate();
        var first = new FakeDatabase(this, ["20260101000000_One"]);
        var second = new FakeDatabase(this, ["20260101000000_One"]);

        Migrator(gate, first, second).MigrateAll();

        Assert.True(gate.IsOpen);
        gate.ThrowIfClosed("Fake Plugin", "notes");
    }

    #endregion

    #region Failure

    [Fact]
    public void AFailedMigrationIsAStartUpFailureNamingThePluginTheContextAndTheMigration()
    {
        var database = new FakeDatabase(this, []);
        var migrationException = new PluginDatabaseMigrationException(database, "20260102000000_Two", Path.Join(_root, "backup.db3"), new InvalidOperationException("duplicate column name: Tag"));

        var (message, exception) = SystemService.DescribePluginStartupFailure(migrationException);

        Assert.Contains(database.PluginID.ToString(), message);
        Assert.Contains("20260102000000_Two", message);
        Assert.Contains(Path.Join(_root, "backup.db3"), message);
        Assert.IsType<StartupFailedException>(exception);
        Assert.Same(migrationException, exception.InnerException);
        Assert.Equal(migrationException.Message, exception.Message);
    }

    [Fact]
    public void AFailedMigrationInTheLateStartStopsTheStartUpAndKeepsEveryDatabaseClosed()
    {
        var gate = new PluginDatabaseGate();
        var database = new FakeDatabase(this, ["20260101000000_One"]) { FailOn = "20260101000000_One" };
        var other = new FakeDatabase(this, ["20260101000000_One"]);

        var failure = SystemService.MigratePluginDatabases(Migrator(gate, database, other), _ => { });

        Assert.NotNull(failure);
        Assert.Equal("20260101000000_One", Assert.IsType<PluginDatabaseMigrationException>(failure.Value.Exception.InnerException).Migration);
        Assert.False(gate.IsOpen);
        Assert.Empty(other.Applied);
    }

    [Fact]
    public void MigratedDatabasesLetTheLateStartGoOn()
    {
        var gate = new PluginDatabaseGate();

        var failure = SystemService.MigratePluginDatabases(Migrator(gate, new FakeDatabase(this, ["20260101000000_One"])), _ => { });

        Assert.Null(failure);
        Assert.True(gate.IsOpen);
    }

    [Fact]
    public void AFailureElsewhereInThePluginsPointsAtTheLogs()
    {
        var failure = new AggregateException("Plugin \"x\" threw while getting ready.");

        var (message, exception) = SystemService.DescribePluginStartupFailure(failure);

        Assert.Equal("Failed to start. Check your logs for more information.", message);
        Assert.Same(failure, exception.InnerException);
    }

    #endregion

    #region Migrator

    [Fact]
    public void AnUpToDateDatabaseIsLeftAlone()
    {
        var database = new FakeDatabase(this, []);
        CreateSqliteFile(database.FilePath);
        var progress = new List<string>();

        Migrator(database).MigrateAll(progress.Add);

        Assert.Empty(database.Applied);
        Assert.Empty(progress);
        Assert.False(Directory.Exists(Path.Join(_root, "backups")));
    }

    [Fact]
    public void AnExistingDatabaseIsCopiedBeforeItsMigrationsApplyInOrder()
    {
        var database = new FakeDatabase(this, ["20260101000000_One", "20260102000000_Two"]);
        CreateSqliteFile(database.FilePath);

        Migrator(database).MigrateAll();

        Assert.Equal(["20260101000000_One", "20260102000000_Two"], database.Applied);
        var backup = Assert.Single(Directory.GetFiles(Path.Join(_root, "backups", database.PluginID.ToString())));
        Assert.Matches(@"notes_\d{14}\.db3$", backup);
        Assert.Equal(["kept"], ReadValues(backup));
    }

    [Fact]
    public void TheCopyOfAnExistingDatabaseAndEachMigrationAreReportedOnALineOfTheirOwn()
    {
        var existing = new FakeDatabase(this, ["20260101000000_One", "20260102000000_Two"]);
        CreateSqliteFile(existing.FilePath);
        var progress = new List<string>();
        var newProgress = new List<string>();

        Migrator(existing).MigrateAll(progress.Add);
        Migrator(new FakeDatabase(this, ["20260101000000_One"])).MigrateAll(newProgress.Add);

        Assert.Equal(3, progress.Count);
        Assert.Contains("20260101000000_One", progress[1]);
        Assert.Contains("20260102000000_Two", progress[2]);
        Assert.Contains("20260101000000_One", Assert.Single(newProgress));
    }

    [Fact]
    public void AFailingMigrationNamesItselfAndTheCopyTakenBeforeIt()
    {
        var database = new FakeDatabase(this, ["20260101000000_One", "20260102000000_Two", "20260103000000_Three"]) { FailOn = "20260102000000_Two" };
        CreateSqliteFile(database.FilePath);

        var ex = Assert.Throws<PluginDatabaseMigrationException>(() => Migrator(database).MigrateAll());

        Assert.Equal(["20260101000000_One"], database.Applied);
        Assert.Equal("20260102000000_Two", ex.Migration);
        Assert.NotNull(ex.BackupFile);
        Assert.True(File.Exists(ex.BackupFile));
        Assert.Equal(database.PluginID, ex.PluginID);
        Assert.Equal(typeof(FakeContext), ex.ContextType);
        Assert.Contains(ex.BackupFile!, ex.Message);
    }

    [Fact]
    public void ANewDatabaseThatFailsSaysToDeleteIt()
    {
        var database = new FakeDatabase(this, ["20260101000000_One"]) { FailOn = "20260101000000_One" };

        var ex = Assert.Throws<PluginDatabaseMigrationException>(() => Migrator(database).MigrateAll());

        Assert.Null(ex.BackupFile);
        Assert.Contains($"deleting \"{database.FilePath}\" starts it over", ex.Message);
    }

    [Fact]
    public void ADatabaseThatCannotBeReadFailsBeforeAnyMigration()
    {
        var database = new FakeDatabase(this, ["20260101000000_One"]) { FailPending = true };

        var ex = Assert.Throws<PluginDatabaseMigrationException>(() => Migrator(database).MigrateAll());

        Assert.Null(ex.Migration);
        Assert.Empty(database.Applied);
        Assert.Contains("could not prepare its database \"notes\"", ex.Message);
    }

    #endregion

    #region Backups

    [Fact]
    public void OnlyTheNewestCopiesAreKept()
    {
        var file = Path.Join(_root, "notes.db3");
        CreateSqliteFile(file);
        var folder = Path.Join(_root, "copies");
        var start = new DateTime(2026, 9, 1, 12, 0, 0);

        var copies = Enumerable.Range(0, 5)
            .Select(minutes => PluginDatabaseBackups.Backup(file, folder, "notes", start.AddMinutes(minutes), keep: 3))
            .ToList();
        File.WriteAllText(Path.Join(folder, "other_20260101000000.db3"), string.Empty);

        Assert.Equal(copies.Skip(2).Order(StringComparer.Ordinal), Directory.GetFiles(folder, "notes_*").Order(StringComparer.Ordinal));
        Assert.True(File.Exists(Path.Join(folder, "other_20260101000000.db3")));
        Assert.Equal(["kept"], ReadValues(copies[^1]));
    }

    [Fact]
    public void TheCoreBackupTakesEveryPluginDatabaseFolderButNotTheCaches()
    {
        var paths = ApplicationPaths();
        var pluginFolder = Path.Join(paths.DatabasePath, Guid.NewGuid().ToString());
        CreateSqliteFile(Path.Join(pluginFolder, "notes.db3"));
        File.WriteAllText(Path.Join(pluginFolder, "legacy-documents.json"), "{}");
        File.WriteAllText(Path.Join(pluginFolder, "notes.db3-wal"), string.Empty);
        Directory.CreateDirectory(Path.Join(paths.CachePath, "some-plugin"));
        File.WriteAllText(Path.Join(paths.CachePath, "some-plugin", "cached.bin"), string.Empty);
        var backupName = Path.Join(_root, "DatabaseBackup", "ShokoServer_193_202609011200");

        var copied = PluginDatabaseBackups.CopyWithBackup(paths, backupName, NullLogger.Instance)!;

        Assert.Equal(backupName + ".plugin-data", copied);
        var copiedFolder = Path.Join(copied, Path.GetFileName(pluginFolder));
        Assert.Equal(["kept"], ReadValues(Path.Join(copiedFolder, "notes.db3")));
        Assert.True(File.Exists(Path.Join(copiedFolder, "legacy-documents.json")));
        Assert.False(File.Exists(Path.Join(copiedFolder, "notes.db3-wal")));
        Assert.DoesNotContain(Directory.GetFiles(copied, "*", SearchOption.AllDirectories), file => file.EndsWith("cached.bin"));
    }

    [Fact]
    public void NoPluginDatabasesMeansNoCopy()
        => Assert.Null(PluginDatabaseBackups.CopyWithBackup(ApplicationPaths(), Path.Join(_root, "backup"), NullLogger.Instance));

    #endregion

    #region Registration

    [Fact]
    public void ARegisteredContextIsKeptInThePluginsDatabaseFolderOnSqlite()
    {
        using var provider = RegisterNotes(out var pluginInfo);
        provider.GetRequiredService<PluginDatabaseGate>().Open();

        var expected = Path.Join(_root, "data", pluginInfo.ID.ToString(), "notes.db3");
        using (var context = provider.GetRequiredService<IDbContextFactory<NotesContext>>().CreateDbContext())
        {
            Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", context.Database.ProviderName);
            Assert.Equal(expected, new SqliteConnectionStringBuilder(context.Database.GetConnectionString()).DataSource);
        }

        using (var scope = provider.CreateScope())
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<NotesContext>());

        var database = Assert.Single(provider.GetServices<IPluginDatabase>());
        Assert.Equal(expected, database.FilePath);
        Assert.Equal(typeof(NotesContext), database.ContextType);
        Assert.Equal(pluginInfo.ID, database.PluginID);
    }

    [Fact]
    public void AContextThatIsNotADbContextIsRefused()
    {
        var services = new ServiceCollection();
        services.AddPluginDbContext<PluginTestDoubles.TestPlugin, NotAContext>("notes");

        var ex = Assert.Throws<InvalidOperationException>(() => PluginDatabaseRegistrar.AddPluginDatabases(services));
        Assert.Contains("not a concrete DbContext", ex.Message);
    }

    [Fact]
    public void TwoDatabasesWithOneNameAreRefused()
    {
        var services = new ServiceCollection();
        services.AddPluginDbContext<PluginTestDoubles.TestPlugin, NotesContext>("notes");
        services.AddPluginDbContext<PluginTestDoubles.TestPlugin, OtherContext>("Notes");

        var ex = Assert.Throws<InvalidOperationException>(() => PluginDatabaseRegistrar.AddPluginDatabases(services));
        Assert.Contains("more than one database named", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../notes")]
    [InlineData("-notes")]
    [InlineData("notes db")]
    public void AnInvalidDatabaseNameIsRefused(string name)
        => Assert.Throws<ArgumentException>(() => new ServiceCollection().AddPluginDbContext<PluginTestDoubles.TestPlugin, NotesContext>(name));

    #endregion

    #region Helpers

    private IApplicationPaths ApplicationPaths()
        => Mock.Of<IApplicationPaths>(paths =>
            paths.DataPath == _root &&
            paths.DatabasePath == Path.Join(_root, "data") &&
            paths.CachePath == Path.Join(_root, "cache") &&
            paths.ConfigurationsPath == Path.Join(_root, "configuration"));

    private PluginDatabaseMigrator Migrator(params IPluginDatabase[] databases)
        => Migrator(new PluginDatabaseGate(), databases);

    private PluginDatabaseMigrator Migrator(PluginDatabaseGate gate, params IPluginDatabase[] databases)
        => new(databases, new PluginDatabaseServer(Constants.DatabaseType.SQLite, string.Empty), ApplicationPaths(), gate, NullLogger<PluginDatabaseMigrator>.Instance)
        {
            BackupDirectory = Path.Join(_root, "backups"),
        };

    private ServiceProvider Services(params IPluginDatabase[] databases)
    {
        var services = new ServiceCollection();
        foreach (var database in databases)
            services.AddSingleton(database);
        services.AddSingleton(ApplicationPaths());
        services.AddSingleton(new PluginDatabaseServer(Constants.DatabaseType.SQLite, string.Empty));
        services.AddSingleton(new PluginDatabaseGate());
        services.AddSingleton<ILogger<PluginDatabaseMigrator>>(NullLogger<PluginDatabaseMigrator>.Instance);
        services.AddSingleton<PluginDatabaseMigrator>();
        return services.BuildServiceProvider();
    }

    private ServiceProvider RegisterNotes(out LocalPluginInfo pluginInfo)
    {
        var info = PluginTestDoubles.InstalledPluginInfo(typeof(PluginTestDoubles.TestPlugin), Guid.NewGuid(), Path.Join(_root, "plugins", "SomePlugin.dll"));
        var pluginManager = new Mock<IPluginManager>();
        pluginManager.Setup(manager => manager.GetPluginInfo<PluginTestDoubles.TestPlugin>()).Returns(info);
        var services = new ServiceCollection();
        services.AddSingleton(ApplicationPaths());
        services.AddSingleton(pluginManager.Object);
        services.AddSingleton(typeof(PluginPaths<>));
        services.AddSingleton(new PluginDatabaseServer(Constants.DatabaseType.SQLite, string.Empty));
        services.AddPluginDbContext<PluginTestDoubles.TestPlugin, NotesContext>("notes");
        PluginDatabaseRegistrar.AddPluginDatabases(services);
        pluginInfo = info;
        return services.BuildServiceProvider();
    }

    private static PluginManager CreateManager(params RecordingPlugin[] plugins)
    {
        var manager = TestPluginManager.Create();
        var entries = TestPluginManager.Plugins(manager);
        foreach (var plugin in plugins)
            entries.Add(PluginTestDoubles.InstalledPluginInfo(typeof(RecordingPlugin), plugin.ID, plugin: plugin));
        return manager;
    }

    private static void Unfreeze()
    {
        MetadataSource.Unfreeze();
        MetadataEntityType.Unfreeze();
    }

    private static void CreateSqliteFile(string file)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = file, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE Kept (Value TEXT NOT NULL); INSERT INTO Kept (Value) VALUES ('kept');";
        command.ExecuteNonQuery();
    }

    private static List<string> ReadValues(string file)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = file, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Kept";
        using var reader = command.ExecuteReader();
        var values = new List<string>();
        while (reader.Read())
            values.Add(reader.GetString(0));
        return values;
    }

    private sealed class RecordingPlugin(List<string> log, string name) : IPlugin
    {
        public Guid ID { get; } = Guid.NewGuid();

        public string Name => name;

        public void Setup(IServiceProvider serviceProvider)
            => log.Add($"setup {name}");

        public void Ready()
            => log.Add($"ready {name}");
    }

    private sealed class FakeDatabase(PluginDatabaseTests tests, IReadOnlyList<string> pending) : IPluginDatabase
    {
        public List<string>? Log { get; init; }

        public string? FailOn { get; init; }

        public bool FailPending { get; init; }

        public List<string> Applied { get; } = [];

        public Guid PluginID { get; } = Guid.NewGuid();

        public string PluginName => "Fake Plugin";

        public string PluginDllName => "FakePlugin";

        public Type ContextType => typeof(FakeContext);

        public string Name => "notes";

        public string FilePath => Path.Join(tests._root, "data", PluginID.ToString(), "notes.db3");

        public PluginDatabaseTarget Target { get; init; } = new(Constants.DatabaseType.SQLite, typeof(FakeContext), null);

        public IReadOnlyList<string> GetPendingMigrations()
            => FailPending ? throw new InvalidOperationException("file is not a database") : pending.Except(Applied).ToList();

        public void Migrate(string migration)
        {
            Log?.Add($"migrate {migration}");
            if (migration == FailOn)
                throw new InvalidOperationException("duplicate column name: Tag");
            Applied.Add(migration);
        }
    }

    private sealed class FakeContext;

    public sealed class NotAContext;

    public sealed class NotesContext(DbContextOptions<NotesContext> options) : DbContext(options);

    public sealed class OtherContext(DbContextOptions<OtherContext> options) : DbContext(options);

    #endregion
}
