using System;
using System.IO;
using System.Linq;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.Settings;

/// <summary>
/// Drives <see cref="SettingsMigrations.MigrateSettings"/> over settings that
/// still name sources by the old enum, to show migration 21 leaves source
/// values behind, and checks the migrations that move TMDB's image settings.
/// </summary>
public sealed class SettingsMigrationSourceValueTests : IDisposable
{
    #region Fixture

    private readonly string _dataPath = Path.Join(Path.GetTempPath(), $"shoko-source-migration-tests-{Guid.NewGuid():N}");

    private readonly IApplicationPaths _applicationPaths;

    public SettingsMigrationSourceValueTests()
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

    private JObject Migrate(string settings)
        => JObject.Parse(SettingsMigrations.MigrateSettings(settings, _applicationPaths));

    private JObject TmdbPluginFile()
        => JObject.Parse(File.ReadAllText(SettingsMigrations.TmdbPluginConfigurationPath(_applicationPaths)));

    private static string[] Strings(JToken? token)
        => token is JArray array ? [.. array.Values<string>().OfType<string>()] : [];

    #endregion

    #region Settings File

    [Fact]
    public void TheLanguageSourceOrders_HoldValues_WithoutNone()
    {
        var migrated = Migrate("""
            {
              "SettingsVersion": 19,
              "Language": {
                "SeriesTitleSourceOrder": ["AniDB", "None", "TMDB", "AniList"],
                "EpisodeTitleSourceOrder": ["TMDB", "AniDB"],
                "DescriptionSourceOrder": ["FanartTV", "TraktTv", "TMDB", "tmdb"]
              }
            }
            """);

        Assert.Equal(["anidb", "tmdb", "anilist"], Strings(migrated["Language"]!["SeriesTitleSourceOrder"]));
        Assert.Equal(["tmdb", "anidb"], Strings(migrated["Language"]!["EpisodeTitleSourceOrder"]));
        Assert.Equal(["fanart-tv", "trakt", "tmdb"], Strings(migrated["Language"]!["DescriptionSourceOrder"]));
    }

    [Fact]
    public void TheImageTemplates_OfTheCoreSourcesStay_AsValues_AndEveryOtherIsDropped()
    {
        var migrated = Migrate("""
            {
              "SettingsVersion": 19,
              "Image": {
                "ImageTemplateUrls": [
                  { "ImageSource": "AniDB", "TemplateUrl": "https://example.com/anidb/{0}" },
                  { "ImageSource": "None", "TemplateUrl": "https://example.com/none/{0}" },
                  { "ImageSource": "AniList", "TemplateUrl": "https://cdn-a.example.com/{0}" },
                  { "ImageSource": "TMDB", "TemplateUrl": "https://example.com/tmdb/{0}" },
                  { "ImageSource": "FanartTV", "TemplateUrl": "https://example.com/fanart/{0}" }
                ],
                "AutoPurge": false
              }
            }
            """);

        // Migration 21 turns the sources into values, then migration 22 drops the template of
        // every source but AniDB and TMDB, a plugin source among them.
        var templates = (JArray)migrated["Image"]!["ImageTemplateUrls"]!;
        Assert.Equal(["anidb", "tmdb"], templates.Select(template => template["ImageSource"]!.Value<string>()));
        Assert.Equal("https://example.com/tmdb/{0}", templates[1]["TemplateUrl"]!.Value<string>());
        Assert.False(migrated["Image"]!["AutoPurge"]!.Value<bool>());
    }

    [Fact]
    public void TmdbsImageSettings_MoveToItsOwnImageEntry_WithTheUsersValues()
    {
        var migrated = Migrate("""
            {
              "SettingsVersion": 19,
              "TMDB": {
                "UserApiKey": "keep-me",
                "ImageLanguageOrder": ["en", "x-main"],
                "AutoDownloadPosters": false,
                "MaxAutoBackdrops": 3,
                "AutoDownloadStudioImages": false
              },
              "Image": { "AutoPurge": true }
            }
            """);

        // Migration 26 then moves what is left of the section to the plugin.
        Assert.Null(migrated["TMDB"]);
        Assert.Equal(["UserApiKey"], TmdbPluginFile().Properties().Select(property => property.Name));
        var entry = (JObject)Assert.Single((JArray)migrated["Image"]!["MetadataSources"]!);
        Assert.Equal("tmdb", entry["Source"]!.Value<string>());
        Assert.Equal(["en", "x-main"], Strings(entry["ImageLanguageOrder"]));
        Assert.False(entry["AutoDownloadPosters"]!.Value<bool>());
        Assert.Equal(3, entry["MaxAutoBackdrops"]!.Value<int>());
        Assert.False(entry["AutoDownloadStudioImages"]!.Value<bool>());
        Assert.True(migrated["Image"]!["AutoPurge"]!.Value<bool>());

        // The entry loads as TMDB's image settings, with the user's values and
        // the defaults for the rest.
        var serializer = JsonSerializer.Create(new() { ObjectCreationHandling = ObjectCreationHandling.Replace });
        var image = migrated["Image"]!.ToObject<ImageSettings>(serializer)!;
        var settings = image.GetMetadataSourceSettings(MetadataSource.TMDB);
        Assert.NotSame(image.MetadataSourceDefaults, settings);
        Assert.False(settings.AutoDownloadPosters);
        Assert.True(settings.AutoDownloadLogos);
        Assert.Equal(3, settings.MaxAutoBackdrops);
        Assert.Equal([Abstractions.Metadata.Enums.TitleLanguage.English, Abstractions.Metadata.Enums.TitleLanguage.Main], settings.ImageLanguageOrder);
    }

    [Fact]
    public void TmdbsImageEntry_GetsBannersOff_SoItsOtherSwitchesStillTurnImagesOff()
    {
        var migrated = Migrate("""
            {
              "SettingsVersion": 19,
              "TMDB": {
                "AutoDownloadBackdrops": false,
                "AutoDownloadPosters": false,
                "AutoDownloadLogos": false,
                "AutoDownloadThumbnails": false,
                "AutoDownloadStaffImages": false,
                "AutoDownloadStudioImages": false
              }
            }
            """);

        var serializer = JsonSerializer.Create(new() { ObjectCreationHandling = ObjectCreationHandling.Replace });
        var image = migrated["Image"]!.ToObject<ImageSettings>(serializer)!;
        var tmdb = image.GetMetadataSourceSettings(MetadataSource.TMDB);
        Assert.False(tmdb.AutoDownloadBanners);
        Assert.False(tmdb.AnyEnabled);
        Assert.False(tmdb.AnyEnabledFor(MetadataEntityType.Series));
        Assert.False(migrated["Image"]!["MetadataSources"]![0]!["AutoDownloadBanners"]!.Value<bool>());
    }

    [Fact]
    public void WithoutTmdbImageSettings_NoEntryIsMade()
    {
        var migrated = Migrate("""{ "SettingsVersion": 19, "TMDB": { "UserApiKey": "keep-me" } }""");

        Assert.Null(migrated["Image"]);
        Assert.Equal("keep-me", TmdbPluginFile()["UserApiKey"]!.Value<string>());
    }

    [Fact]
    public void AnOldFile_GetsTheDefaultSourceOrdersAsValues()
    {
        var migrated = Migrate("""{ "SettingsVersion": 9, "Language": {} }""");

        Assert.Equal(["anidb", "tmdb"], Strings(migrated["Language"]!["SeriesTitleSourceOrder"]));
        Assert.Equal(["anidb", "tmdb"], Strings(migrated["Language"]!["DescriptionSourceOrder"]));
    }

    [Fact]
    public void TheMigratedSettings_LoadAsSources()
    {
        var migrated = Migrate("""
            {
              "SettingsVersion": 19,
              "Language": { "SeriesTitleSourceOrder": ["TMDB", "LocallyGenerated"] }
            }
            """);

        var serializer = JsonSerializer.Create(new() { ObjectCreationHandling = ObjectCreationHandling.Replace });
        var language = migrated["Language"]!.ToObject<LanguageSettings>(serializer)!;
        Assert.Equal([MetadataSource.TMDB, MetadataSource.Generated], language.SeriesTitleSourceOrder);
    }

    #endregion
}
