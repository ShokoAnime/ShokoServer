using System;
using System.IO;
using System.Linq;
using Moq;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.Settings;

/// <summary>
/// Drives the real <see cref="SettingsMigrations.MigrateSettings"/> over a
/// settings document shaped like the one on disk, to show what migration 20
/// carries over from <see cref="TMDBSettings"/>, and that it leaves the
/// <c>Anilist</c> section older versions wrote alone.
/// </summary>
public sealed class SettingsMigrationAutoLinkTests : IDisposable
{
    #region Fixture

    private readonly string _dataPath = Path.Join(Path.GetTempPath(), $"shoko-migration-tests-{Guid.NewGuid():N}");

    private readonly IApplicationPaths _applicationPaths;

    public SettingsMigrationAutoLinkTests()
    {
        Directory.CreateDirectory(_dataPath);

        var applicationPaths = new Mock<IApplicationPaths>(MockBehavior.Loose);
        applicationPaths.SetupGet(paths => paths.DataPath).Returns(_dataPath);
        applicationPaths.SetupGet(paths => paths.ConfigurationsPath).Returns(Path.Join(_dataPath, "configurations"));
        _applicationPaths = applicationPaths.Object;
    }

    public void Dispose()
    {
        if (Directory.Exists(_dataPath))
            Directory.Delete(_dataPath, true);
    }

    /// <summary>
    /// A settings document at the version just before migration 20, carrying the
    /// keys it is meant to take over plus a sibling it must leave alone, and an
    /// <c>Anilist</c> section as older versions wrote it, which no migration reads.
    /// </summary>
    private static string Version19(string? tmdbBody = null)
        => $$"""
        {
          "SettingsVersion": 19,
          "TMDB": {
            "UserApiKey": "keep-me",
            {{tmdbBody}}
            "AutoDownloadPosters": true
          },
          "Anilist": {
            "AutoLink": true,
            "AutoLinkRestricted": false,
            "AutoPurgeUnlinkedAfterDays": 30,
            "RateLimit": 42,
            "AutoDownloadPosters": false
          }
        }
        """;

    #endregion

    #region Carry-over

    [Fact]
    public void ADeliberateOptOut_IsCarriedOverAsFalse_NotAsAbsent()
    {
        var settings = Version19("\"AutoLink\": false, \"AutoLinkRestricted\": false,");

        SettingsMigrations.MigrateSettings(settings, _applicationPaths);

        var carried = SettingsMigrations.ReadAutoLinkCarryOver(_dataPath);
        Assert.Equal([MetadataSource.TMDB], carried.Keys);
        Assert.False(carried[MetadataSource.TMDB].AutoLink);
        Assert.False(carried[MetadataSource.TMDB].AutoLinkRestricted);
    }

    [Fact]
    public void AbsentKeys_CarryNothingOver()
    {
        SettingsMigrations.MigrateSettings(Version19(), _applicationPaths);

        Assert.Empty(SettingsMigrations.ReadAutoLinkCarryOver(_dataPath));
        Assert.False(File.Exists(SettingsMigrations.AutoLinkCarryOverPath(_dataPath)));
    }

    [Fact]
    public void AnAbsentParent_CarriesNothingOver()
    {
        SettingsMigrations.MigrateSettings("""{ "SettingsVersion": 19, "Web": { "Port": 8111 } }""", _applicationPaths);

        Assert.Empty(SettingsMigrations.ReadAutoLinkCarryOver(_dataPath));
    }

    #endregion

    #region Rewrite

    [Fact]
    public void TheLegacyKeysAreDropped_AndTheirSiblingsSurvive()
    {
        var settings = Version19("\"AutoLink\": false, \"AutoLinkRestricted\": true,");

        var migrated = JObject.Parse(SettingsMigrations.MigrateSettings(settings, _applicationPaths));

        // Migration 26 then moves what is left of the section to the plugin.
        Assert.Null(migrated["TMDB"]);
        var plugin = JObject.Parse(File.ReadAllText(SettingsMigrations.TmdbPluginConfigurationPath(_applicationPaths)));
        Assert.Null(plugin["AutoLink"]);
        Assert.Null(plugin["AutoLinkRestricted"]);
        Assert.Equal("keep-me", plugin["UserApiKey"]!.Value<string>());
        // Migration 23 moves TMDB's image switches to its image entry.
        Assert.True(migrated["Image"]!["MetadataSources"]![0]!["AutoDownloadPosters"]!.Value<bool>());
    }

    [Fact]
    public void TheAnilistSection_IsLeftForTheNextSaveToDrop()
    {
        var settings = Version19("\"AutoLink\": false,");

        var migrated = JObject.Parse(SettingsMigrations.MigrateSettings(settings, _applicationPaths));

        Assert.True(JToken.DeepEquals(JObject.Parse(settings)["Anilist"], migrated["Anilist"]));
    }

    [Fact]
    public void TheVersionIsStamped_ToTheLatestMigration()
    {
        var migrated = JObject.Parse(SettingsMigrations.MigrateSettings(Version19(), _applicationPaths));

        Assert.Equal(SettingsMigrations.Version, migrated["SettingsVersion"]!.Value<int>());
    }

    [Fact]
    public void ThePreMigrationDocument_IsBackedUp()
    {
        SettingsMigrations.MigrateSettings(Version19("\"AutoLink\": false,"), _applicationPaths);

        var backup = Directory.GetFiles(Path.Combine(_dataPath, "SettingsBackup"), "settings-server.v19.*").Single();
        Assert.Contains("\"AutoLink\": false", File.ReadAllText(backup));
    }

    #endregion

    #region Surviving a failed first boot

    /// <summary>
    /// The keys are gone from the stamped document, so a second boot does not
    /// migrate again. The carry-over has to outlive that boot on its own, until
    /// the provider manager has applied and saved it.
    /// </summary>
    [Fact]
    public void TheCarryOver_OutlivesTheBootThatWroteIt_UntilCleared()
    {
        var migrated = SettingsMigrations.MigrateSettings(Version19("\"AutoLink\": false,"), _applicationPaths);

        // A second boot, reading back what the first one wrote, without having seeded.
        var again = SettingsMigrations.MigrateSettings(migrated, _applicationPaths);
        Assert.Equal(migrated, again);
        Assert.False(SettingsMigrations.ReadAutoLinkCarryOver(_dataPath)[MetadataSource.TMDB].AutoLink);

        SettingsMigrations.ClearAutoLinkCarryOver(_dataPath);
        Assert.Empty(SettingsMigrations.ReadAutoLinkCarryOver(_dataPath));
    }

    #endregion
}
