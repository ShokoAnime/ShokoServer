using System;
using System.IO;
using Moq;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.Settings;

/// <summary>
/// Drives the real <see cref="SettingsMigrations.MigrateSettings"/> over a
/// settings document shaped like the one on disk, to show what migration 25
/// takes out of the settings and carries over for the action scheduler.
/// </summary>
public sealed class SettingsMigrationUpdateFrequencyTests : IDisposable
{
    #region Fixture

    private readonly string _dataPath = Path.Join(Path.GetTempPath(), $"shoko-migration-tests-{Guid.NewGuid():N}");

    private readonly IApplicationPaths _applicationPaths;

    public SettingsMigrationUpdateFrequencyTests()
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
    /// A settings document at the version just before migration 25.
    /// </summary>
    private static string Version24(string anidbBody, string mylistBody = "", string updatesBody = "")
        => $$"""
        {
          "SettingsVersion": 24,
          "AniDb": {
            {{anidbBody}}
            "MyList": {
              {{mylistBody}}
              "RetainedBackupCount": 30
            },
            "Notification_HandleMovedFiles": true
          },
          "Plugins": {
            "Updates": {
              {{updatesBody}}
              "IsAutoSyncEnabled": true
            }
          }
        }
        """;

    #endregion

    #region Carry-over

    [Fact]
    public void EveryFrequency_IsCarriedOverInHours_AndRemoved()
    {
        var settings = Version24(
            """
            "Calendar_UpdateFrequency": "HoursTwelve",
            "Anime_UpdateFrequency": "EveryHour",
            "File_UpdateFrequency": "WeekOne",
            "Notification_UpdateFrequency": "Never",
            """,
            "\"UpdateFrequency\": \"MonthOne\",",
            "\"AutoUpdateFrequency\": \"HoursSix\","
        );

        var migrated = JObject.Parse(SettingsMigrations.MigrateSettings(settings, _applicationPaths));

        var carried = SettingsMigrations.ReadUpdateFrequencyCarryOver(_dataPath);
        Assert.Equal(12, carried[SettingsMigrations.AnidbCalendarFrequency]);
        Assert.Equal(1, carried[SettingsMigrations.AnidbAnimeFrequency]);
        Assert.Equal(24 * 7, carried[SettingsMigrations.AnidbFileFrequency]);
        Assert.Equal(0, carried[SettingsMigrations.AnidbNotificationFrequency]);
        Assert.Equal(24 * 30, carried[SettingsMigrations.AnidbMylistFrequency]);
        Assert.Equal(6, carried[SettingsMigrations.PluginUpdatesFrequency]);

        var anidb = (JObject)migrated["AniDb"]!;
        Assert.Null(anidb.Property("Calendar_UpdateFrequency"));
        Assert.Null(anidb.Property("Anime_UpdateFrequency"));
        Assert.Null(anidb.Property("File_UpdateFrequency"));
        Assert.Null(anidb.Property("Notification_UpdateFrequency"));
        Assert.Null(((JObject)anidb["MyList"]!).Property("UpdateFrequency"));
        Assert.Null(((JObject)migrated["Plugins"]!["Updates"]!).Property("AutoUpdateFrequency"));

        Assert.True(anidb["Notification_HandleMovedFiles"]!.Value<bool>());
        Assert.Equal(30, anidb["MyList"]!["RetainedBackupCount"]!.Value<int>());
        Assert.True(migrated["Plugins"]!["Updates"]!["IsAutoSyncEnabled"]!.Value<bool>());
    }

    [Fact]
    public void ANumericFrequency_IsReadAsTheEnumValue()
    {
        // 4 is Daily, 1 is Never.
        SettingsMigrations.MigrateSettings(Version24("\"File_UpdateFrequency\": 4, \"Calendar_UpdateFrequency\": 1,"), _applicationPaths);

        var carried = SettingsMigrations.ReadUpdateFrequencyCarryOver(_dataPath);
        Assert.Equal(24, carried[SettingsMigrations.AnidbFileFrequency]);
        Assert.Equal(0, carried[SettingsMigrations.AnidbCalendarFrequency]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\"File_UpdateFrequency\": \"Sometimes\",")]
    public void AnAbsentOrUnreadableFrequency_IsNotCarriedOver(string body)
    {
        SettingsMigrations.MigrateSettings(Version24(body), _applicationPaths);

        Assert.Empty(SettingsMigrations.ReadUpdateFrequencyCarryOver(_dataPath));
        Assert.False(File.Exists(SettingsMigrations.UpdateFrequencyCarryOverPath(_dataPath)));
    }

    [Fact]
    public void Clearing_RemovesTheCarryOver()
    {
        SettingsMigrations.MigrateSettings(Version24("\"File_UpdateFrequency\": \"Daily\","), _applicationPaths);
        Assert.NotEmpty(SettingsMigrations.ReadUpdateFrequencyCarryOver(_dataPath));

        SettingsMigrations.ClearUpdateFrequencyCarryOver(_dataPath);

        Assert.Empty(SettingsMigrations.ReadUpdateFrequencyCarryOver(_dataPath));
    }

    #endregion
}
