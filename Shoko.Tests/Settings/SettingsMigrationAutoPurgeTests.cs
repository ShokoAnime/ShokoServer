using System;
using System.IO;
using Moq;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.Settings;

/// <summary>
/// Drives <see cref="SettingsMigrations.MigrateSettings"/> over settings at
/// version 19, to show migration 24 moves TMDB's automatic purge setting to
/// the metadata settings.
/// </summary>
public sealed class SettingsMigrationAutoPurgeTests : IDisposable
{
    #region Fixture

    private readonly string _dataPath = Path.Join(Path.GetTempPath(), $"shoko-auto-purge-migration-tests-{Guid.NewGuid():N}");

    private readonly IApplicationPaths _applicationPaths;

    public SettingsMigrationAutoPurgeTests()
    {
        Directory.CreateDirectory(_dataPath);

        var applicationPaths = new Mock<IApplicationPaths>(MockBehavior.Loose);
        applicationPaths.SetupGet(paths => paths.DataPath).Returns(_dataPath);
        _applicationPaths = applicationPaths.Object;
    }

    public void Dispose()
    {
        if (Directory.Exists(_dataPath))
            Directory.Delete(_dataPath, true);
    }

    private JObject Migrate(string settings)
        => JObject.Parse(SettingsMigrations.MigrateSettings(settings, _applicationPaths));

    #endregion

    #region Migration 24

    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    public void TmdbsValue_MovesToTheMetadataSettings(int days)
    {
        var migrated = Migrate($$"""
            {
              "SettingsVersion": 19,
              "TMDB": { "AutoPurgeUnlinkedAfterDays": {{days}}, "UserApiKey": "keep-me" }
            }
            """);

        Assert.Null(migrated["TMDB"]!["AutoPurgeUnlinkedAfterDays"]);
        Assert.Equal("keep-me", migrated["TMDB"]!["UserApiKey"]!.Value<string>());
        Assert.Equal(days, migrated["Metadata"]!["AutoPurgeUnlinkedAfterDays"]!.Value<int>());
    }

    [Fact]
    public void WithoutTmdbsValue_TheDefaultApplies()
    {
        var migrated = Migrate("""
            {
              "SettingsVersion": 19,
              "TMDB": { "UserApiKey": "keep-me" }
            }
            """);

        Assert.Null(migrated["Metadata"]);
    }

    #endregion
}
