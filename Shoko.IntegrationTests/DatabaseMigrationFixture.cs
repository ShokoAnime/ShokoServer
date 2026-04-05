#nullable enable
using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NLog.Extensions.Logging;
using Quartz;
using Shoko.Abstractions.Core.Services;
using Shoko.Server.Databases;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling;
using Shoko.Server.Services;

namespace Shoko.IntegrationTests;

/// <summary>
/// Bootstraps a minimal DI container and runs the full database initialization
/// (schema creation + all migrations + initial data) against whichever database
/// backend is selected via environment variables.
///
/// Environment variables (mirrors <see cref="Shoko.Server.Settings.DatabaseSettings"/>):
///   DB_TYPE              – SQLite (default), SQLServer, MySQL
///   DB_SQLITE_DIRECTORY  – directory for the SQLite file (auto-set to a temp dir when not provided)
///   DB_HOST              – hostname[:port] for SQL Server / MySQL
///   DB_USER              – username
///   DB_PASS              – password
///   DB_NAME              – database / schema name
/// </summary>
public sealed class DatabaseMigrationFixture : IDisposable
{
    public bool Success { get; private set; }
    public string? FailureMessage { get; private set; }

    private readonly string? _tempSqliteDir;

    public DatabaseMigrationFixture()
    {
        var dbType = Environment.GetEnvironmentVariable("DB_TYPE") ?? "SQLite";
        var isSqlite = dbType is "SQLite" or "0";

        if (isSqlite && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DB_SQLITE_DIRECTORY")))
        {
            _tempSqliteDir = Path.Combine(Path.GetTempPath(), $"shoko-integration-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempSqliteDir);
            Environment.SetEnvironmentVariable("DB_SQLITE_DIRECTORY", _tempSqliteDir);
        }

        // SystemService() constructor bootstraps Utils.SettingsProvider, NLog, ApplicationPaths,
        // and PluginManager — all of which must exist before the repositories are resolved.
        var systemService = new SystemService();

        var services = new ServiceCollection();
        services.AddLogging(b =>
        {
            b.ClearProviders();
            b.SetMinimumLevel(LogLevel.Trace);
            b.AddNLog();
        });
        services.AddSingleton(systemService);
        services.AddSingleton<ISystemService>(_ => systemService);
        // JobFactory is needed by CrossRef_File_EpisodeRepository and AniDB_GroupStatusRepository.
        // Register a minimal QuartzOptions and the factory itself without the full Quartz stack.
        services.AddSingleton<IOptions<QuartzOptions>>(_ => Options.Create(new QuartzOptions()));
        services.AddSingleton<JobFactory>();
        // Registers DatabaseFactory, RepoFactory, and all 50+ repository singletons.
        services.AddRepositories();

        var sp = services.BuildServiceProvider();
        var databaseFactory = sp.GetRequiredService<DatabaseFactory>();
        var repoFactory = sp.GetRequiredService<RepoFactory>();

        Success = systemService.InitializeDatabase(databaseFactory, repoFactory, CancellationToken.None);
        if (!Success)
            FailureMessage = systemService.StartupFailedException?.Message ?? "InitializeDatabase returned false";
    }

    public void Dispose()
    {
        if (_tempSqliteDir is not null && Directory.Exists(_tempSqliteDir))
            Directory.Delete(_tempSqliteDir, recursive: true);
    }
}
