using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Abstractions.Video.Streaming;
using Shoko.Server.Plugin;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
///   The core has parts of its own in the stream pipeline -- the built-in scrobble observer is
///   playback behaviour that used to be hardcoded into the <c>/Stream</c> endpoints -- and they
///   reach a caller through the same path a plugin's do: <c>IPluginManager</c> supplies the
///   plugin info each part is attributed to, and settings supply whether it is enabled. Both
///   steps fail quietly. A core part that cannot be attributed would take playback with it at
///   startup rather than throw in a test nobody runs, and an enable-default read with
///   <c>TryGetValue(...) &amp;&amp; value</c> turns "no setting yet" into "off" -- which for the
///   scrobble observer means every player that marks watched through the legacy query parameter
///   stops doing so on upgrade, with nothing logged.
/// </summary>
public class VideoStreamPipelineObserverTests
{
    [Fact]
    public void CoreObserver_IsEnabledWhenTheSettingHasNeverBeenWritten()
    {
        var pipeline = CreatePipeline(PluginTestDoubles.CorePluginInfo(typeof(CorePlugin), CorePlugin.StaticID));
        pipeline.AddObserverParts([new TestObserver()]);

        var observer = Assert.Single(pipeline.GetAvailableObservers());
        Assert.True(observer.Enabled);
    }

    [Fact]
    public void PluginObserver_IsDisabledWhenTheSettingHasNeverBeenWritten()
    {
        var pipeline = CreatePipeline(PluginTestDoubles.InstalledPluginInfo(typeof(PluginTestDoubles.TestPlugin), Guid.NewGuid()));
        pipeline.AddObserverParts([new TestObserver()]);

        var observer = Assert.Single(pipeline.GetAvailableObservers());
        Assert.False(observer.Enabled);
    }

    [Fact]
    public void CoreObserver_IsStillDisabledByAnExplicitFalseSetting()
        => AssertEnabledWithSetting(false, PluginTestDoubles.CorePluginInfo(typeof(CorePlugin), CorePlugin.StaticID));

    [Fact]
    public void PluginObserver_IsStillEnabledByAnExplicitTrueSetting()
        => AssertEnabledWithSetting(true, PluginTestDoubles.InstalledPluginInfo(typeof(PluginTestDoubles.TestPlugin), Guid.NewGuid()));

    private static void AssertEnabledWithSetting(bool enabled, LocalPluginInfo pluginInfo)
    {
        var settings = new VideoStreamPipelineSettings();
        var pipeline = CreatePipeline(pluginInfo, settings);
        var id = GetPartID(typeof(TestObserver));
        settings.ObserverEnabled[id] = enabled;

        pipeline.AddObserverParts([new TestObserver()]);

        var observer = Assert.Single(pipeline.GetAvailableObservers());
        Assert.Equal(enabled, observer.Enabled);
    }

    /// <summary>The id a part is stored and configured under, mirrored from the service so the
    /// settings-driven cases address the observer by the same key it is registered under.</summary>
    private static string GetPartID(Type type)
    {
        var assemblyName = type.Assembly.GetName().Name ?? string.Empty;
        var typeName = type.FullName ?? type.Name;
        if (assemblyName.Length > 0 && typeName.StartsWith(assemblyName + ".", StringComparison.Ordinal))
            typeName = typeName[(assemblyName.Length + 1)..];
        return assemblyName.Length > 0 ? $"{assemblyName}:{typeName}" : typeName;
    }

    private static VideoStreamPipelineService CreatePipeline(LocalPluginInfo pluginInfo,
                                                             VideoStreamPipelineSettings? settings = null)
    {
        settings ??= new VideoStreamPipelineSettings();
        var configurationInfo = (ConfigurationInfo)RuntimeHelpers.GetUninitializedObject(typeof(ConfigurationInfo));
        var configurationService = new Mock<IConfigurationService>();
        configurationService.Setup(c => c.GetConfigurationInfo<VideoStreamPipelineSettings>()).Returns(configurationInfo);
        configurationService.Setup(c => c.Load(It.IsAny<ConfigurationInfo>(), It.IsAny<bool>())).Returns(settings);

        var pluginManager = new Mock<IPluginManager>();
        pluginManager.Setup(p => p.GetPluginInfo(It.IsAny<Assembly>())).Returns(pluginInfo);

        return new VideoStreamPipelineService(
            NullLogger<VideoStreamPipelineService>.Instance,
            configurationService.Object,
            new ConfigurationProvider<VideoStreamPipelineSettings>(configurationService.Object),
            pluginManager.Object);
    }

    private sealed class TestObserver : IPlaybackObserver
    {
        public string Name => "Test Observer";

        public Task OnPlaybackProgress(PlaybackProgressContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
