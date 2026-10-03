using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using MessagePack;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Video.Relocation;
using Shoko.Abstractions.Video.Services;
using Shoko.Plugin.WebAOM;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Services;
using Shoko.Server.Services.Configuration;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Plugin.WebAOM;

/// <summary>
///   Checks that the presets and settings of the WebAOM renamer the core
///   shipped carry over to the renamer in its bundled plugin.
/// </summary>
public sealed class WebAOMRenamerMigrationTests : IDisposable
{
    #region Fixture

    private readonly string _dataPath = Path.Join(Path.GetTempPath(), $"shoko-webaom-migration-tests-{Guid.NewGuid():N}");

    private readonly ConfigurationService _configurationService;

    private readonly RelocationProviderInfo _provider;

    public WebAOMRenamerMigrationTests()
    {
        var applicationPaths = new Mock<IApplicationPaths>(MockBehavior.Loose);
        applicationPaths.SetupGet(paths => paths.DataPath).Returns(_dataPath);
        applicationPaths.SetupGet(paths => paths.ConfigurationsPath).Returns(Path.Join(_dataPath, "configurations"));
        _configurationService = new(NullLoggerFactory.Instance, applicationPaths.Object, new Mock<IPluginManager>(MockBehavior.Loose).Object);

        // Only the type of the configuration is read on the paths under test.
        var configurationInfo = (ConfigurationInfo)RuntimeHelpers.GetUninitializedObject(typeof(ConfigurationInfo));
        typeof(ConfigurationInfo).GetProperty(nameof(ConfigurationInfo.Type))!.SetValue(configurationInfo, typeof(WebAOMSettings));
        _provider = new()
        {
            ID = WebAOMRenamerMigration.ProviderID,
            Version = new(1, 0, 0),
            Name = "WebAOM Renamer",
            Description = string.Empty,
            Provider = new WebAOMRenamer(NullLogger<WebAOMRenamer>.Instance, Mock.Of<IVideoRelocationService>()),
            ConfigurationInfo = configurationInfo,
            PluginInfo = PluginTestDoubles.InstalledPluginInfo(typeof(Shoko.Plugin.WebAOM.Plugin), WebAOMRenamerMigration.PluginID, "Shoko.Plugin.WebAOM.dll"),
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_dataPath))
            Directory.Delete(_dataPath, true);
    }

    /// <summary>
    ///   The renamer settings as v5 stored them, packed with their type.
    /// </summary>
    public sealed class V5WebAOMSettings
    {
        public int MaxEpisodeLength { get; set; }

        public bool GroupAwareSorting { get; set; }

        public string Script { get; set; } = string.Empty;
    }

    #endregion

    #region IDs

    [Fact]
    public void ProviderID_IsTheOneTheServerGivesThePluginsRenamer()
    {
        Assert.Equal(new Shoko.Plugin.WebAOM.Plugin().ID, WebAOMRenamerMigration.PluginID);
        Assert.Equal(VideoRelocationService.GetProviderID(typeof(WebAOMRenamer).FullName!, WebAOMRenamerMigration.PluginID), WebAOMRenamerMigration.ProviderID);
    }

    [Fact]
    public void LegacyProviderID_IsTheOnePresetsSavedBeforeTheMoveHold()
        => Assert.Equal(new Guid("ce69f6d2-a102-5907-9338-f8c812c6955b"), WebAOMRenamerMigration.LegacyProviderID);

    #endregion

    #region Presets

    [Fact]
    public void RepointPresets_MovesTheCoreRenamersPresets_AndKeepsTheirSettings()
    {
        // The JSON the core's own settings type was saved as.
        var json = """{"MaxEpisodeLength":50,"GroupAwareSorting":true,"Script":"DO ADD '%ann'"}""";
        var webAom = new StoredRelocationPreset { Name = "Default", ProviderID = WebAOMRenamerMigration.LegacyProviderID, Configuration = Encoding.UTF8.GetBytes(json) };
        var other = new StoredRelocationPreset { Name = "Other", ProviderID = Guid.NewGuid() };
        var otherProviderID = other.ProviderID;

        var changed = WebAOMRenamerMigration.RepointPresets([webAom, other]);

        Assert.Same(webAom, Assert.Single(changed));
        Assert.Equal(WebAOMRenamerMigration.ProviderID, webAom.ProviderID);
        Assert.Equal(otherProviderID, other.ProviderID);
        var settings = Assert.IsType<WebAOMSettings>(_configurationService.Deserialize(_provider.ConfigurationInfo!, Encoding.UTF8.GetString(webAom.Configuration!)));
        Assert.Equal(50, settings.MaxEpisodeLength);
        Assert.True(settings.GroupAwareSorting);
        Assert.Equal("DO ADD '%ann'", settings.Script);
    }

    #endregion

    #region Settings

    [Fact]
    public void CarryOverPackedSettings_ReadsTheSettingsOfAV5RenamerInstanceByName()
    {
        var packed = MessagePackSerializer.Typeless.Serialize(new V5WebAOMSettings { MaxEpisodeLength = 50, GroupAwareSorting = true, Script = "DO ADD '%ann'" }, cancellationToken: TestContext.Current.CancellationToken);

        var settings = Assert.IsType<WebAOMSettings>(WebAOMRenamerMigration.CarryOverPackedSettings(_configurationService, _provider, packed));

        Assert.Equal(50, settings.MaxEpisodeLength);
        Assert.True(settings.GroupAwareSorting);
        Assert.Equal("DO ADD '%ann'", settings.Script);
    }

    [Fact]
    public void CarryOverSettings_KeepsTheDefaultsForWhatWasNotStored()
    {
        var defaults = (WebAOMSettings)_configurationService.New(_provider.ConfigurationInfo!);

        var settings = Assert.IsType<WebAOMSettings>(WebAOMRenamerMigration.CarryOverSettings(_configurationService, _provider, """{"Script":"DO ADD '%ann'","GroupAwareSorting":true}"""));

        Assert.Equal(defaults.MaxEpisodeLength, settings.MaxEpisodeLength);
        Assert.True(settings.GroupAwareSorting);
        Assert.Equal("DO ADD '%ann'", settings.Script);
    }

    #endregion
}
