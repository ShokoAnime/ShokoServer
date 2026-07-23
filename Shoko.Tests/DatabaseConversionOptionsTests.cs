using System;
using System.IO;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Server.Server;
using Xunit;

namespace Shoko.Tests;

public class DatabaseConversionOptionsTests
{
    public static TheoryData<string[]> ConversionModeArguments =>
    [
        ["--convert-db"],
        ["--home", "/tmp/shoko", "--convert-db"],
    ];

    [Theory]
    [MemberData(nameof(ConversionModeArguments))]
    public void ShouldDetectConversionModeIndependentOfPosition(string[] args)
    {
        var detected = DatabaseConversionOptions.TryParse(args, out var options);

        Assert.True(detected);
        Assert.NotNull(options);
    }

    [Fact]
    public void ShouldParseKnownConversionArgumentsWhileIgnoringHostArguments()
    {
        var args = new[]
        {
            "--convert-db",
            "--home", "/tmp/shoko",
            "--source-type", "mariadb",
            "--source-connection-string", "Server=127.0.0.1;Database=shoko;",
            "--target-file", "/tmp/Shoko.db3",
            "--overwrite",
        };

        var detected = DatabaseConversionOptions.TryParse(args, out var options);

        Assert.True(detected);
        Assert.NotNull(options);
        Assert.Equal(DatabaseConversionService.SourceDatabaseType.MySql, options.SourceType);
        Assert.Equal("Server=127.0.0.1;Database=shoko;", options.SourceConnectionString);
        Assert.Equal("/tmp/Shoko.db3", options.TargetFile);
        Assert.True(options.Overwrite);
    }

    [Fact]
    public void ShouldNotDetectConversionModeWhenModeArgumentIsMissing()
    {
        var detected = DatabaseConversionOptions.TryParse(["--home", "/tmp/shoko"], out var options);

        Assert.False(detected);
        Assert.Null(options);
    }

    [Fact]
    public void ShouldNotDetectConversionModeWhenHomeValueMatchesModeToken()
    {
        var detected = DatabaseConversionOptions.TryParse(["--home", "convert-db"], out var options);

        Assert.False(detected);
        Assert.Null(options);
    }

    [Fact]
    public void ShouldNotTreatTargetFileValueAsModeTokenWithoutActualModeArgument()
    {
        var detected = DatabaseConversionOptions.TryParse(["--target-file", "convert-db"], out var options);

        Assert.False(detected);
        Assert.Null(options);
    }

    [Fact]
    public void ShouldNotDetectConversionModeWhenFutureOptionValueMatchesModeToken()
    {
        var detected = DatabaseConversionOptions.TryParse(["--some-future-option", "convert-db"], out var options);

        Assert.False(detected);
        Assert.Null(options);
    }

    [Fact]
    public void ShouldNotDetectConversionModeForBareConvertDbToken()
    {
        var detected = DatabaseConversionOptions.TryParse(["convert-db"], out var options);

        Assert.False(detected);
        Assert.Null(options);
    }

    [Fact]
    public void ShouldUseConfiguredSettingsAsSourceWhenSourceArgsAreOmitted()
    {
        var settings = new ServerSettings
        {
            Database =
            {
                Type = Constants.DatabaseType.MySQL,
                Host = "db.example:3307",
                Username = "user",
                Password = "pass",
                Schema = "shoko",
            }
        };
        var options = new DatabaseConversionOptions
        {
            TargetFile = "/tmp/Shoko.db3",
        };

        var resolved = DatabaseConversionService.ResolveSource(options, settings);

        Assert.Equal(DatabaseConversionService.SourceDatabaseType.MySql, resolved.SourceType);
        Assert.Equal("Server=db.example;Port=3307;Database=shoko;User ID=user;Password=pass;Default Command Timeout=3600;Allow User Variables=true", resolved.SourceConnectionString);
    }

    [Fact]
    public void ExplicitSourceArgsShouldOverrideConfiguredSettings()
    {
        var settings = new ServerSettings
        {
            Database =
            {
                Type = Constants.DatabaseType.MySQL,
                Host = "db.example:3307",
                Username = "user",
                Password = "pass",
                Schema = "shoko",
            }
        };
        var options = new DatabaseConversionOptions
        {
            SourceType = DatabaseConversionService.SourceDatabaseType.SqlServer,
            SourceTypeProvided = true,
            SourceConnectionString = "Server=override;Database=override;",
            SourceConnectionStringProvided = true,
            TargetFile = "/tmp/Shoko.db3",
        };

        var resolved = DatabaseConversionService.ResolveSource(options, settings);

        Assert.Equal(DatabaseConversionService.SourceDatabaseType.SqlServer, resolved.SourceType);
        Assert.Equal("Server=override;Database=override;", resolved.SourceConnectionString);
    }

    [Fact]
    public void ConfiguredSqliteSourceShouldFailClearly()
    {
        var settings = new ServerSettings
        {
            Database =
            {
                Type = Constants.DatabaseType.SQLite,
            }
        };
        var options = new DatabaseConversionOptions
        {
            TargetFile = "/tmp/Shoko.db3",
        };

        var ex = Assert.Throws<InvalidOperationException>(() => DatabaseConversionService.ResolveSource(options, settings));

        Assert.Contains("SQLite", ex.Message);
    }

    [Fact]
    public void OmittedTargetFileShouldResolveToDefaultSqlitePath()
    {
        var settings = new ServerSettings
        {
            Database =
            {
                MySqliteDirectory = "SQLite",
                SQLite_DatabaseFile = "Shoko.db3",
            }
        };
        var options = new DatabaseConversionOptions();

        var resolved = DatabaseConversionService.ResolveTarget(options, settings);

        Assert.Equal(Path.GetFullPath(Path.Combine(ApplicationPaths.StaticDataPath, "SQLite", "Shoko.db3")), resolved.TargetFile);
    }

    [Fact]
    public void ExplicitTargetFileShouldOverrideDefaultSqlitePath()
    {
        var settings = new ServerSettings
        {
            Database =
            {
                MySqliteDirectory = "SQLite",
                SQLite_DatabaseFile = "Shoko.db3",
            }
        };
        var options = new DatabaseConversionOptions
        {
            TargetFile = "/tmp/custom.db3",
            TargetFileProvided = true,
        };

        var resolved = DatabaseConversionService.ResolveTarget(options, settings);

        Assert.Equal(Path.GetFullPath("/tmp/custom.db3"), resolved.TargetFile);
    }

    [Fact]
    public void ExistingTargetFileShouldFailWithoutOverwrite()
    {
        var targetFile = Path.Combine(Path.GetTempPath(), $"shoko-converter-target-{Guid.NewGuid():N}.db3");
        try
        {
            File.WriteAllText(targetFile, "existing");

            var ex = Assert.Throws<InvalidOperationException>(() => DatabaseConversionService.PrepareTargetPath(targetFile, overwrite: false));

            Assert.Contains("already exists", ex.Message);
        }
        finally
        {
            if (File.Exists(targetFile))
            {
                File.Delete(targetFile);
            }
        }
    }

    [Fact]
    public void PreparedConversionRuntimeShouldIsolateQuartzAndTargetBootstrapPaths()
    {
        var realQuartzConnectionString = $"Data Source={Path.Combine(ApplicationPaths.StaticDataPath, "SQLite", "Quartz.db3")};Mode=ReadWriteCreate;Pooling=True";
        var settings = new ServerSettings
        {
            Database =
            {
                Type = Constants.DatabaseType.MySQL,
                Host = "db.example:3306",
                Username = "user",
                Password = "pass",
                Schema = "shoko",
            },
            Quartz =
            {
                DatabaseType = Constants.DatabaseType.SQLServer,
                ConnectionString = realQuartzConnectionString,
            },
        };
        var options = new DatabaseConversionOptions
        {
            TargetFile = Path.Combine(Path.GetTempPath(), $"shoko-converter-target-{Guid.NewGuid():N}.db3"),
            TargetFileProvided = true,
        };

        try
        {
            var runtime = DatabaseConversionService.PrepareRuntime(options, settings);
            var runtimeSettings = (ServerSettings)runtime.RuntimeSettingsProvider.GetSettings(copy: true);

            Assert.Equal(Constants.DatabaseType.SQLite, runtimeSettings.Database.Type);
            Assert.Contains(runtime.PreparedTargetFile, runtimeSettings.Database.OverrideConnectionString, StringComparison.Ordinal);
            Assert.Equal(Constants.DatabaseType.SQLite, runtimeSettings.Quartz.DatabaseType);
            Assert.Contains(runtime.TemporaryHomePath, runtimeSettings.Quartz.ConnectionString, StringComparison.Ordinal);
            Assert.DoesNotContain(realQuartzConnectionString, runtimeSettings.Quartz.ConnectionString, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(options.TargetFile))
            {
                File.Delete(options.TargetFile);
            }
        }
    }
}
