#nullable enable
using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Hosting;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.IntegrationTests;

/// <summary>
/// Starts the full Shoko Server bootstrap against an isolated temp directory,
/// then waits for database initialization to complete.
///
/// Database backend is selected via environment variables that mirror
/// <see cref="DatabaseSettings"/>:
///   DB_TYPE   – SQLite (default), SQLServer, MySQL
///   DB_HOST   – hostname[:port] for SQL Server / MySQL
///   DB_USER   – username
///   DB_PASS   – password
///   DB_NAME   – database / schema name
/// </summary>
public sealed class DatabaseMigrationFixture : IDisposable
{
    public bool Success { get; private set; }
    public string? FailureMessage { get; private set; }

    private readonly string _tempDir;
    private IHost? _host;

    public DatabaseMigrationFixture()
    {
        // Isolated data directory so this run doesn't touch a real Shoko install.
        _tempDir = Path.Combine(Path.GetTempPath(), $"shoko-integration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        // SHOKO_HOME controls Utils.ApplicationPath. Must be set before SystemService() reads it.
        // Forward slashes avoid bad JSON escape sequences when the config service parses env vars.
        Environment.SetEnvironmentVariable("SHOKO_HOME", _tempDir.Replace('\\', '/'));

        // SystemService() bootstraps Utils.SettingsProvider with default settings (FirstRun=true).
        // No settings file yet — defaults are valid and pass schema validation.
        var systemService = new SystemService();

        // Mutate the live settings: disable first-run, inject fake AniDB credentials so the
        // settings custom-validator is satisfied, and move the web port away from 8111 so this
        // doesn't conflict with a real Shoko instance.
        var settings = Utils.SettingsProvider.GetSettings();
        settings.FirstRun = false;
        settings.AniDb.Username = "integration-test";
        settings.AniDb.Password = "integration-test";
        settings.Web.Port = 28111;
        Utils.SettingsProvider.SaveSettings(settings);

        var started = new ManualResetEventSlim(false);
        systemService.Started += (_, _) =>
        {
            Success = true;
            started.Set();
        };
        systemService.StartupFailed += (_, args) =>
        {
            Success = false;
            FailureMessage = args.Exception?.Message ?? "Startup failed";
            started.Set();
        };

        // StartAsync builds the full DI container (including all services used by database fixes)
        // and sets Utils.ServiceContainer before LateStart triggers InitializeDatabase.
        _host = systemService.StartAsync().GetAwaiter().GetResult();
        if (_host is null)
        {
            Success = false;
            FailureMessage = systemService.StartupFailedException?.Message ?? "StartAsync returned null host";
            return;
        }

        // LateStart runs InitializeDatabase as a fire-and-forget task; wait for its completion event.
        if (!started.Wait(TimeSpan.FromMinutes(10)))
        {
            Success = false;
            FailureMessage = "Database initialization timed out after 10 minutes";
        }
    }

    public void Dispose()
    {
        try
        {
            _host?.StopAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
        }
        catch
        {
            // Best-effort shutdown; don't mask test failures.
        }

        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // SQLite connections may still be draining; ignore cleanup errors.
        }
    }
}
