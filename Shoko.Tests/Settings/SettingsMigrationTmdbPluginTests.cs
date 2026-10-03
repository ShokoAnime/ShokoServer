using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Moq;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Plugin;
using Shoko.Plugin.Tmdb;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.Settings;

/// <summary>
/// Drives <see cref="SettingsMigrations.MigrateTmdbSettingsToPlugin"/>, settings
/// migration 26, over settings shaped like the file on disk, and checks that
/// the plugin's configuration reads what it writes.
/// </summary>
public sealed class SettingsMigrationTmdbPluginTests : IDisposable
{
    #region Fixture

    private readonly string _rootPath = Path.Join(Path.GetTempPath(), $"shoko-tmdb-migration-tests-{Guid.NewGuid():N}");

    private readonly IApplicationPaths _applicationPaths;

    public SettingsMigrationTmdbPluginTests()
    {
        var applicationPaths = new Mock<IApplicationPaths>(MockBehavior.Loose);
        applicationPaths.SetupGet(paths => paths.ConfigurationsPath).Returns(Path.Join(_rootPath, "configurations"));
        applicationPaths.SetupGet(paths => paths.DataPath).Returns(Path.Join(_rootPath, "data"));
        _applicationPaths = applicationPaths.Object;
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, true);
    }

    private string PluginFile => SettingsMigrations.TmdbPluginConfigurationPath(_applicationPaths);

    private JObject Migrate(string settings)
        => JObject.Parse(SettingsMigrations.MigrateTmdbSettingsToPlugin(settings, _applicationPaths));

    private static string WithTmdb(string tmdbBody, string imageBody = "")
        => $$"""
        {
          "SettingsVersion": 25,
          "Image": {
            {{imageBody}}
            "AutoPurge": false
          },
          "TMDB": {
            {{tmdbBody}}
          }
        }
        """;

    private static JArray Templates(JObject settings)
        => settings["Image"]?["ImageTemplateUrls"] as JArray ?? [];

    #endregion

    #region Migration

    [Fact]
    public void AFullSection_MovesToThePluginFile_AndItsCdnUrlBecomesTheTemplate()
    {
        var migrated = Migrate(WithTmdb(
            """
            "ConsiderExistingOtherLinks": true,
            "DownloadAllTitles": true,
            "DownloadAllOverviews": true,
            "DownloadAllContentRatings": true,
            "AutoDownloadCrewAndCast": true,
            "AutoDownloadCollections": true,
            "AutoDownloadAlternateOrdering": true,
            "AutoDownloadNetworks": true,
            "UserApiKey": "user-key",
            "ImageCdnUrl": "https://cdn.example.com/t/p",
            "IncrementalChangesWindowDays": 7,
            "AutoSearchShowCandidateCount": 3,
            "AutoSearchMovieCandidateCount": 2,
            "RateLimit": { "MaxRequestsPerWindow": 20, "WindowDurationMs": 2000 },
            "Stale": 1
            """
        ));

        Assert.Null(migrated.Property("TMDB"));
        Assert.False(migrated["Image"]!["AutoPurge"]!.Value<bool>());
        var template = Assert.Single(Templates(migrated));
        Assert.Equal("tmdb", template["ImageSource"]!.Value<string>());
        Assert.Equal("https://cdn.example.com/t/p/original/{0}", template["TemplateUrl"]!.Value<string>());

        // Every key the plugin reads is carried with the user's value, and nothing else.
        var file = JObject.Parse(File.ReadAllText(PluginFile));
        Assert.Equal(SettingsMigrations.TmdbPluginConfigurationKeys.Order(), file.Properties().Select(property => property.Name).Order());
        Assert.Equal("user-key", file["UserApiKey"]!.Value<string>());
        Assert.Equal(7, file["IncrementalChangesWindowDays"]!.Value<int>());
        Assert.Equal(20, file["RateLimit"]!["MaxRequestsPerWindow"]!.Value<int>());

        // The file reads back as the plugin's configuration.
        var configuration = file.ToObject<TmdbConfiguration>()!;
        Assert.True(configuration.AutoDownloadNetworks);
        Assert.Equal(2, configuration.AutoSearchMovieCandidateCount);
        Assert.Equal(2000, configuration.RateLimit.WindowDurationMs);
    }

    [Fact]
    public void AnEmptySection_IsRemoved_AndWritesNoFile()
    {
        var migrated = Migrate(WithTmdb(string.Empty));

        Assert.Null(migrated.Property("TMDB"));
        Assert.Empty(Templates(migrated));
        Assert.False(File.Exists(PluginFile));
    }

    [Fact]
    public void AnExistingPluginFile_IsKept_AndTheSectionStillMoves()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PluginFile)!);
        File.WriteAllText(PluginFile, """{ "AutoDownloadNetworks": false }""");

        var migrated = Migrate(WithTmdb(
            """
            "AutoDownloadNetworks": true,
            "ImageCdnUrl": "https://cdn.example.com/{0}"
            """
        ));

        Assert.Equal("""{ "AutoDownloadNetworks": false }""", File.ReadAllText(PluginFile));
        Assert.Null(migrated.Property("TMDB"));
        Assert.Equal("https://cdn.example.com/{0}", Assert.Single(Templates(migrated))["TemplateUrl"]!.Value<string>());
    }

    [Fact]
    public void KeysLeftToTheEnvironment_StayOutOfThePluginFile()
    {
        // TMDB_API_KEY, TMDB_IMAGE_CDN_URL, TMDB_CHANGES_WINDOW_DAYS and the rate limit
        // variables are applied when loading and reverted on save, so the file never holds them.
        var migrated = Migrate(WithTmdb(
            """
            "ConsiderExistingOtherLinks": true,
            "RateLimit": {}
            """
        ));

        var file = JObject.Parse(File.ReadAllText(PluginFile));
        Assert.Equal(["ConsiderExistingOtherLinks", "RateLimit"], file.Properties().Select(property => property.Name));
        Assert.Empty((JObject)file["RateLimit"]!);
        Assert.Empty(Templates(migrated));
    }

    [Fact]
    public void TheMigration_RunsAsMigration26()
    {
        var migrated = JObject.Parse(SettingsMigrations.MigrateSettings(WithTmdb("\"AutoDownloadNetworks\": true"), _applicationPaths));

        Assert.Null(migrated.Property("TMDB"));
        Assert.True(JObject.Parse(File.ReadAllText(PluginFile))["AutoDownloadNetworks"]!.Value<bool>());
    }

    [Fact]
    public void ASecondRun_ChangesNothing()
    {
        var once = SettingsMigrations.MigrateTmdbSettingsToPlugin(WithTmdb("\"DownloadAllTitles\": true"), _applicationPaths);
        var written = File.ReadAllText(PluginFile);

        var twice = SettingsMigrations.MigrateTmdbSettingsToPlugin(once, _applicationPaths);

        Assert.Equal(once, twice);
        Assert.Equal(written, File.ReadAllText(PluginFile));
    }

    [Theory]
    [InlineData("\"https://cdn.example.com/\"", "https://cdn.example.com/original/{0}")]
    [InlineData("\"https://cdn.example.com/w500/{0}\"", "https://cdn.example.com/w500/{0}")]
    [InlineData("\"ftp://cdn.example.com/\"", null)]
    [InlineData("\"   \"", null)]
    [InlineData("null", null)]
    public void TheCdnUrl_BecomesATemplate_OnlyWhenTheImageManagerWouldUseIt(string value, string? expected)
    {
        var migrated = Migrate(WithTmdb($"\"ImageCdnUrl\": {value}"));

        Assert.Equal(expected, Templates(migrated).SingleOrDefault()?["TemplateUrl"]!.Value<string>());
        Assert.False(File.Exists(PluginFile));
    }

    [Fact]
    public void ATemplateTheUserSet_WinsOverTheCdnUrl()
    {
        var migrated = Migrate(WithTmdb(
            "\"ImageCdnUrl\": \"https://cdn.example.com/\"",
            "\"ImageTemplateUrls\": [{ \"ImageSource\": \"tmdb\", \"TemplateUrl\": \"https://mine.example.com/{0}\" }],"
        ));

        Assert.Equal("https://mine.example.com/{0}", Assert.Single(Templates(migrated))["TemplateUrl"]!.Value<string>());
    }

    #endregion

    #region Parity

    [Fact]
    public void ThePluginIdentity_MatchesWhatTheMigrationWrites()
    {
        var pluginID = new Shoko.Plugin.Tmdb.Plugin().ID;
        Assert.Equal(pluginID, SettingsMigrations.TmdbPluginID);
        var embedded = typeof(TmdbConfiguration).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(attribute => attribute.Key == "PackageID");
        Assert.Equal(pluginID, Guid.Parse(embedded.Value!));

        var storage = typeof(TmdbConfiguration).GetCustomAttribute<StorageLocationAttribute>()!;
        Assert.Equal(Path.GetFileName(PluginFile), storage.FileName + ".json");
    }

    [Fact]
    public void ThePluginConfiguration_ReadsEveryKeyTheMigrationCarries()
        => Assert.Equal(JObject.FromObject(new TmdbConfiguration()).Properties().Select(property => property.Name).Order(), SettingsMigrations.TmdbPluginConfigurationKeys.Order());

    [Fact]
    public void TheCarriedApiKey_IsMaskedOnTheWayOut()
    {
        var masked = ConfigurationSecrets.Mask(JObject.FromObject(new TmdbConfiguration { UserApiKey = "user-key" }), typeof(TmdbConfiguration), [1, 2, 3]);

        Assert.True(ConfigurationSecrets.IsMasked(masked["UserApiKey"]!.Value<string>()));
    }

    [Fact]
    public void TheConfiguredApiKey_IsPreferred_AndABlankOneIsNoKey()
    {
        Assert.Equal("user-key", TmdbApiKey.Resolve(new() { UserApiKey = "user-key" }));
        Assert.Equal(TmdbApiKey.Resolve(new()), TmdbApiKey.Resolve(new() { UserApiKey = " " }));
    }

    #endregion
}
