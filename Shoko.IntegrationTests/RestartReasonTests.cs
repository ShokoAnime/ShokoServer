using System;
using System.Linq;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Server.Plugin;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Checks the restart reasons of the started server: none for its plugins' state, a reason raised
/// and cleared through <see cref="ISystemService"/> by the core or by the <see cref="TestPlugins"/>
/// it loaded (and refused for a plugin it did not load), and the one configuration reason following
/// a restart-only setting there and back, which the enabled plugins setting leaves out.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class RestartReasonTests(DatabaseMigrationFixture fixture)
{
    [Fact]
    public void NoPluginStateReasonStandsAfterStartup()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var systemService = fixture.Services.GetRequiredService<ISystemService>();

        Assert.DoesNotContain(systemService.RestartReasons, reason => reason.Source is RestartReasonSource.PluginState);
    }

    [Fact]
    public void TheEnabledPluginsSettingRaisesNoConfigurationReason()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var systemService = fixture.Services.GetRequiredService<ISystemService>();
        var configurationService = fixture.Services.GetRequiredService<IConfigurationService>();
        var settingsProvider = fixture.Services.GetRequiredService<ISettingsProvider>();
        const string pluginName = "RestartReasonTestPlugin";
        try
        {
            var settings = settingsProvider.GetSettings(copy: true);
            settings.Plugins.EnabledPlugins[pluginName] = false;
            settingsProvider.SaveSettings(settings);

            // Still pending on the configuration, but left to the plugin state reason.
            Assert.Contains(configurationService.RestartPendingFor.Values, members => members.Contains(RestartReasonTracker.EnabledPluginsMember));
            Assert.DoesNotContain(systemService.RestartReasons, standing => standing.Source is RestartReasonSource.Configuration);
        }
        finally
        {
            var settings = settingsProvider.GetSettings(copy: true);
            settings.Plugins.EnabledPlugins.Remove(pluginName);
            settingsProvider.SaveSettings(settings);
        }

        Assert.DoesNotContain(configurationService.RestartPendingFor.Values, members => members.Contains(RestartReasonTracker.EnabledPluginsMember));
    }

    [Fact]
    public void ARestartOnlySettingRaisesAConfigurationReasonUntilItIsChangedBack()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var systemService = fixture.Services.GetRequiredService<ISystemService>();
        var configurationService = fixture.Services.GetRequiredService<IConfigurationService>();
        var settingsProvider = fixture.Services.GetRequiredService<ISettingsProvider>();
        var original = settingsProvider.GetSettings().ThreadPoolMinThreads;
        try
        {
            var settings = settingsProvider.GetSettings(copy: true);
            settings.ThreadPoolMinThreads = original + 3;
            settingsProvider.SaveSettings(settings);

            // One reason for every configuration; the members stay on the configuration service.
            var reason = Assert.Single(systemService.RestartReasons, standing => standing.Source is RestartReasonSource.Configuration);
            Assert.Equal(CorePlugin.StaticID, reason.PluginID);
            Assert.Equal(RestartReasonTracker.ConfigurationKey, reason.Key);
            Assert.Contains(configurationService.RestartPendingFor.Values, members => members.Any(member => member.EndsWith(nameof(IServerSettings.ThreadPoolMinThreads), StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            var settings = settingsProvider.GetSettings(copy: true);
            settings.ThreadPoolMinThreads = original;
            settingsProvider.SaveSettings(settings);
        }

        Assert.DoesNotContain(systemService.RestartReasons, standing => standing.Source is RestartReasonSource.Configuration);
    }

    [Fact]
    public void ARestartAskedForATypeThatIsNotAnActivePluginIsRefused()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var systemService = fixture.Services.GetRequiredService<ISystemService>();

        // The type is a plugin, but not one the server loaded.
        Assert.Throws<InvalidOperationException>(() => systemService.RequireRestart<UnloadedPlugin>("Asked for by a test."));
    }

    [Fact]
    public void TheCorePluginRaisesAReasonByItsType()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var systemService = fixture.Services.GetRequiredService<ISystemService>();
        IRestartRequirement? restart = null;
        try
        {
            restart = systemService.RequireRestart<CorePlugin>("The core needs a restart for a test.");

            Assert.Equal(CorePlugin.StaticID, restart.Plugin.ID);
            Assert.Contains(restart.Reason, systemService.RestartReasons);
        }
        finally
        {
            restart?.Dispose();
        }
    }

    public static TheoryData<string, string> PluginCalls => new()
    {
        { TestPlugins.FolderPluginAssembly, "Direct" },
        { TestPlugins.FolderPluginAssembly, "AfterAwait" },
        { TestPlugins.FolderPluginAssembly, "ThroughLibrary" },
        { TestPlugins.SingleFilePluginAssembly, "Direct" },
        { TestPlugins.SingleFilePluginAssembly, "AfterAwait" },
    };

    [Theory]
    [MemberData(nameof(PluginCalls))]
    public async Task ARestartAskedForByALoadedPluginIsHeldForThatPlugin(string pluginAssembly, string method)
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var systemService = fixture.Services.GetRequiredService<ISystemService>();
        var pluginInfo = GetTestPlugin(pluginAssembly);
        var caller = pluginInfo.PluginType!.Assembly.GetType($"{pluginAssembly}.RestartCaller", throwOnError: true)!.GetMethod(method)!;
        var description = $"{pluginAssembly} asked for a restart through {method}.";
        IRestartRequirement? restart = null;
        try
        {
            var result = caller.Invoke(null, [systemService, description]);
            restart = result is Task<IRestartRequirement> task ? await task : (IRestartRequirement)result!;

            Assert.Equal(pluginInfo.ID, restart.Plugin.ID);
            Assert.Equal(pluginInfo.ID, restart.Reason.PluginID);
            Assert.Contains(systemService.RestartReasons, standing => standing.Source is RestartReasonSource.Plugin && standing.Key == restart.Reason.Key
                && standing.PluginID == pluginInfo.ID && standing.Description == description);

            restart.Dispose();
            Assert.False(restart.IsHeld);
            Assert.DoesNotContain(systemService.RestartReasons, standing => standing.Key == restart.Reason.Key);
        }
        finally
        {
            restart?.Dispose();
        }
    }

    /// <summary>
    /// Gets one of the <see cref="TestPlugins"/>, checking the server loaded it the way it loads
    /// every enabled plugin: active, and in the default load context.
    /// </summary>
    /// <param name="pluginAssembly">The name of the plugin's main assembly.</param>
    /// <returns>The plugin.</returns>
    private LocalPluginInfo GetTestPlugin(string pluginAssembly)
    {
        var pluginId = pluginAssembly is TestPlugins.FolderPluginAssembly ? TestPlugins.FolderPluginID : TestPlugins.SingleFilePluginID;
        var pluginInfo = fixture.Services.GetRequiredService<IPluginManager>().GetPluginInfo(pluginId);
        Assert.NotNull(pluginInfo);
        Assert.True(pluginInfo.IsActive);
        Assert.Equal(pluginAssembly, pluginInfo.PluginType?.Assembly.GetName().Name);
        Assert.Same(AssemblyLoadContext.Default, AssemblyLoadContext.GetLoadContext(pluginInfo.PluginType!.Assembly));
        return pluginInfo;
    }

    private sealed class UnloadedPlugin : IPlugin
    {
        public Guid ID { get; } = new("0f6b1e0c-4a52-4d7e-9a37-3c1f0b8e2d94");

        public string Name { get; } = "Unloaded Plugin";
    }
}
