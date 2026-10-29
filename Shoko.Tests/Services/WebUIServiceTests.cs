using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Moq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Abstractions.Web;
using Shoko.Abstractions.Web.Enums;
using Shoko.Abstractions.Web.Events;
using Shoko.Server.API.FileProviders;
using Shoko.Server.Plugin;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
///   The web UI service picks the webUI to serve, refuses mount paths
///   no webUI may have, and brings the installed release in line at start.
/// </summary>
public sealed class WebUIServiceTests : IDisposable
{
    private static readonly Guid _pluginID = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private readonly string _root = Directory.CreateTempSubdirectory("web-uis-").FullName;

    private readonly ServerSettings _settings = new();

    private WebUIServiceSettings _serviceSettings = new();

    private readonly LocalPluginInfo _pluginInfo = PluginTestDoubles.InstalledPluginInfo(typeof(PluginTestDoubles.TestPlugin), _pluginID);

    public void Dispose() => Directory.Delete(_root, true);

    #region Mount Paths

    [Theory]
    [InlineData("", "/")]
    [InlineData("/", "/")]
    [InlineData("webui", "/webui")]
    [InlineData("/noren/", "/noren")]
    [InlineData("/apix", "/apix")]
    public void MountPath_IsNormalized(string path, string expected)
    {
        Assert.True(WebUIService.TryNormalizeMountPath(path, ["/api", "/plugin", "/signalr", "/swagger"], out var normalized, out _));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("/api")]
    [InlineData("/API/v3")]
    [InlineData("/plugin/noren")]
    [InlineData("/signalr")]
    [InlineData("/swagger")]
    [InlineData("/a/../b")]
    [InlineData("/a?b")]
    public void MountPath_IsRefused(string path)
        => Assert.False(WebUIService.TryNormalizeMountPath(path, ["/api", "/plugin", "/signalr", "/swagger"], out _, out _));

    [Fact]
    public void WebUIAtReservedPath_FailsItsPlugin()
    {
        var failed = new List<LocalPluginInfo>();
        var service = Create(new FakeWebUI("/api/noren"), failed);

        Assert.Equal([_pluginInfo], failed);
        Assert.Single(service.GetAvailableProviders());
    }

    #endregion

    #region Active Web UI

    [Fact]
    public void PluginWebUI_IsServedAutomatically()
    {
        var webUI = new FakeWebUI("/noren");

        Assert.Same(webUI, Create(webUI).ActiveProvider.Provider);
    }

    [Fact]
    public void SelectedCoreWebUI_IsServedOverPluginWebUI()
    {
        var service = Create(new FakeWebUI("/noren"));
        service.SelectProvider(service.GetProviderInfo<CoreWebUIProvider>());

        Assert.IsType<CoreWebUIProvider>(Create(new FakeWebUI("/noren")).ActiveProvider.Provider);
    }

    [Fact]
    public void MissingSelection_FallsBackToPluginWebUI()
    {
        _serviceSettings = new() { SelectedProviderID = Guid.NewGuid() };
        var webUI = new FakeWebUI("/noren");

        Assert.Same(webUI, Create(webUI).ActiveProvider.Provider);
    }

    [Fact]
    public void FailedPluginWebUI_FallsBackToCoreWebUI()
    {
        var webUI = new FakeWebUI("/noren");
        var service = Create(webUI);
        _serviceSettings = new() { SelectedProviderID = service.GetProviderInfo(webUI).ID };
        _pluginInfo.FailureReason = "It broke.";

        Assert.IsType<CoreWebUIProvider>(service.ActiveProvider.Provider);
        var picked = service.GetProviderInfo(webUI);
        Assert.True(picked.IsSelected);
        Assert.False(picked.IsActive);
    }

    [Fact]
    public async Task SelectingAWebUI_SwitchesAtOnce()
    {
        var webUI = new FakeWebUI("/noren");
        var service = Create(webUI);
        Assert.Same(webUI, service.ActiveProvider.Provider);
        var changed = new TaskCompletionSource<WebUIActiveProviderChangedEventArgs>();
        service.ActiveProviderChanged += (_, eventArgs) => changed.TrySetResult(eventArgs);

        service.SelectProvider(service.GetProviderInfo<CoreWebUIProvider>());

        Assert.IsType<CoreWebUIProvider>(service.ActiveProvider.Provider);
        var core = Assert.Single(service.GetAvailableProviders(), info => info is { IsActive: true, IsSelected: true });
        Assert.IsType<CoreWebUIProvider>(core.Provider);
        Assert.IsType<CoreWebUIProvider>((await changed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Provider.Provider);
    }

    [Fact]
    public void TurningServingOff_AppliesAtOnce()
    {
        var service = Create(new FakeWebUI("/noren"));
        Assert.True(service.GetState().IsEnabled);

        service.IsEnabled = false;

        Assert.False(service.GetState().IsEnabled);
    }

    [Fact]
    public void ChangedMountPath_IsPickedUp_UnlessRefused()
    {
        var webUI = new FakeWebUI("/noren");
        var service = Create(webUI);
        Assert.Equal("/noren", service.GetState().MountPath);

        webUI.MountPath = "/other/";
        Assert.Equal("/other", service.GetState().MountPath);

        webUI.MountPath = "/api/noren";
        Assert.Equal("/other", service.GetState().MountPath);
    }

    #endregion

    #region Host Overrides

    [Fact]
    public async Task HostOverride_ServesItsWebUIForItsHostOnly()
    {
        var webUI = new FakeWebUI("/noren");
        var service = Create(webUI);
        var changed = new TaskCompletionSource<WebUIActiveProviderChangedEventArgs>();
        service.ActiveProviderChanged += (_, eventArgs) => changed.TrySetResult(eventArgs);

        service.SetHostOverrides([new() { Host = "Core.Example.com", ProviderID = service.GetProviderInfo<CoreWebUIProvider>().ID }]);

        Assert.IsType<CoreWebUIProvider>(service.GetState(new HostString("core.example.com:8111")).Active.Provider);
        Assert.Same(webUI, service.GetState(new HostString("other.example.com")).Active.Provider);
        Assert.Same(webUI, service.ActiveProvider.Provider);
        Assert.All(service.GetAvailableProviders(), info => Assert.True(info.IsActive));
        var eventArgs = await changed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal("core.example.com", eventArgs.Host);
        Assert.IsType<CoreWebUIProvider>(eventArgs.Provider.Provider);
    }

    [Fact]
    public void HostOverride_WithFailedWebUI_ServesTheDefault()
    {
        var webUI = new FakeWebUI("/noren");
        var service = Create(webUI);
        service.SelectProvider(service.GetProviderInfo<CoreWebUIProvider>());
        service.SetHostOverrides([new() { Host = "noren.example.com", ProviderID = service.GetProviderInfo(webUI).ID }]);
        Assert.Same(webUI, service.GetState(new HostString("noren.example.com")).Active.Provider);

        _pluginInfo.FailureReason = "It broke.";

        Assert.IsType<CoreWebUIProvider>(service.GetState(new HostString("noren.example.com")).Active.Provider);
    }

    [Fact]
    public void HostOverride_ForUnknownWebUI_IsRefused()
    {
        var service = Create(new FakeWebUI("/noren"));

        Assert.Throws<ArgumentException>(() => service.SetHostOverrides([new() { Host = "example.com", ProviderID = Guid.NewGuid() }]));
        Assert.Empty(service.HostOverrides);
    }

    [Fact]
    public void StartPolicy_CoversHostOverrideWebUIs()
    {
        var webUI = new FakeWebUI("/noren") { BundledRelease = Release(1, 0, isBundled: true) };
        var ids = Create(webUI);
        _serviceSettings = new()
        {
            SelectedProviderID = ids.GetProviderInfo<CoreWebUIProvider>().ID,
            HostOverrides = [new() { Host = "noren.example.com", ProviderID = ids.GetProviderInfo(webUI).ID }],
        };

        Create(webUI).ApplyStartPolicy();

        Assert.Single(webUI.Installed);
    }

    #endregion

    #region Start Policy

    [Fact]
    public void NothingInstalled_InstallsBundledRelease()
    {
        var webUI = new FakeWebUI("/noren") { BundledRelease = Release(1, 0, isBundled: true) };

        Create(webUI).ApplyStartPolicy();

        Assert.Equal(new Version(1, 0, 0), Assert.Single(webUI.Installed).Version);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void NewerBundledRelease_ReplacesOnlyWhenAllowed(bool autoReplace, bool replaced)
    {
        _settings.Web.AutoReplaceWebUIWithIncluded = autoReplace;
        var webUI = new FakeWebUI("/noren") { CurrentRelease = Release(1, 0), BundledRelease = Release(1, 1, isBundled: true) };

        Create(webUI).ApplyStartPolicy();

        Assert.Equal(replaced, webUI.Installed.Count is 1);
    }

    [Fact]
    public void IncompatibleRelease_IsReplacedByBundledRelease()
    {
        _settings.Web.AutoReplaceWebUIWithIncluded = false;
        var current = Release(1, 1);
        current.IncompatibleReason = "It needs a newer plugin.";
        var webUI = new FakeWebUI("/noren") { CurrentRelease = current, BundledRelease = Release(1, 0, isBundled: true) };

        Create(webUI).ApplyStartPolicy();

        Assert.Single(webUI.Installed);
    }

    [Fact]
    public void DebugRelease_IsNeverReplacedByStableRelease()
    {
        var current = Release(1, 0, channel: ReleaseChannel.Debug);
        current.IncompatibleReason = "It needs a newer plugin.";
        var webUI = new FakeWebUI("/noren") { CurrentRelease = current, BundledRelease = Release(2, 0, isBundled: true) };

        Create(webUI).ApplyStartPolicy();

        Assert.Empty(webUI.Installed);
    }

    #endregion

    #region Helpers

    private static WebUIReleaseInformation Release(int major, int minor, bool isBundled = false, ReleaseChannel channel = ReleaseChannel.Stable)
        => new()
        {
            Version = new(major, minor, 0),
            Description = null,
            SourceRevision = string.Empty,
            ReleaseTag = $"v{major}.{minor}.0",
            Channel = channel,
            ReleasedAt = DateTime.UnixEpoch,
            IsBundled = isBundled,
        };

    private WebUIService Create(IWebUIProvider webUI, List<LocalPluginInfo>? failed = null)
    {
        var settingsProvider = new Mock<ISettingsProvider>();
        settingsProvider.Setup(provider => provider.GetSettings(It.IsAny<bool>())).Returns(_settings);

        var applicationPaths = new Mock<IApplicationPaths>();
        applicationPaths.SetupGet(paths => paths.DataPath).Returns(_root);
        applicationPaths.SetupGet(paths => paths.ApplicationPath).Returns(Path.Combine(_root, "app"));

        var configurationInfo = (ConfigurationInfo)RuntimeHelpers.GetUninitializedObject(typeof(ConfigurationInfo));
        var configurationService = new Mock<IConfigurationService>();
        configurationService.Setup(service => service.GetConfigurationInfo<WebUIServiceSettings>()).Returns(configurationInfo);
        configurationService.Setup(service => service.Load(It.IsAny<ConfigurationInfo>(), It.IsAny<bool>())).Returns(() => _serviceSettings);
        configurationService.Setup(service => service.Save(It.IsAny<WebUIServiceSettings>()))
            .Callback((WebUIServiceSettings settings) => _serviceSettings = settings)
            .Returns(true);

        var coreInfo = PluginTestDoubles.CorePluginInfo(typeof(CorePlugin), CorePlugin.StaticID);
        var pluginManager = new Mock<IPluginManager>();
        pluginManager.Setup(manager => manager.GetPluginInfo(It.IsAny<Assembly>()))
            .Returns((Assembly assembly) => assembly == typeof(CorePlugin).Assembly ? coreInfo : _pluginInfo);

        var systemService = new Mock<ISystemService>();
        systemService.SetupGet(service => service.Version).Returns(_pluginInfo.Version);

        var httpClientFactory = Mock.Of<IHttpClientFactory>();
        var coreProvider = new CoreWebUIProvider(
            NullLogger<CoreWebUIProvider>.Instance,
            settingsProvider.Object,
            applicationPaths.Object,
            httpClientFactory,
            new ReleaseManifestReader(NullLogger<ReleaseManifestReader>.Instance, httpClientFactory, applicationPaths.Object)
        );
        var service = new WebUIService(
            NullLogger<WebUIService>.Instance,
            settingsProvider.Object,
            new ConfigurationProvider<WebUIServiceSettings>(configurationService.Object),
            new ConfigurationProvider<ServerSettings>(configurationService.Object),
            configurationService.Object,
            pluginManager.Object,
            systemService.Object,
            coreProvider
        );
        service.AddParts([coreProvider, webUI], (pluginInfo, _) => failed?.Add(pluginInfo));
        return service;
    }

    /// <summary>
    ///   A plugin's web UI that records what it is asked to install.
    /// </summary>
    private sealed class FakeWebUI(string mountPath) : IWebUIProvider
    {
        public List<WebUIReleaseInformation> Installed { get; } = [];

        public string Name => "Fake Web UI";

        public string MountPath { get; set; } = mountPath;

        public IReadOnlyDictionary<WebUILinkType, string> Links { get; } = new Dictionary<WebUILinkType, string>();

        public WebUIReleaseInformation? CurrentRelease { get; set; }

        public WebUIReleaseInformation? BundledRelease { get; set; }

        public event EventHandler? ReleaseInstalled { add { } remove { } }

        public Task<IReadOnlyList<WebUIReleaseInformation>> GetReleases(bool force = false, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WebUIReleaseInformation>>([]);

        public Task<WebUIInstallResult> InstallRelease(WebUIReleaseInformation release, CancellationToken cancellationToken = default)
        {
            Installed.Add(release);
            CurrentRelease = release;
            return Task.FromResult(WebUIInstallResult.Installed);
        }

        public IFileInfo GetFileInfo(string subpath) => new NotFoundFileInfo(subpath);

        public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;

        public IChangeToken Watch(string filter) => NullChangeToken.Singleton;
    }

    #endregion
}
