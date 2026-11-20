using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Server.Plugin;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Plugin;

/// <summary>
///   A configuration a plugin registers through
///   <see cref="ConfigurationRegistrationExtensions.AddConfiguration{TPlugin, TConfig}(IServiceCollection)"/>
///   is handed to the configuration service once the plugin is initialized.
/// </summary>
public class PluginConfigurationRegistrationTests
{
    private static readonly MethodInfo _takeRegistrations = typeof(PluginManager)
        .GetMethod("TakeConfigurationRegistrations", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly MethodInfo _getConfigurationTypes = typeof(PluginManager)
        .GetMethod("GetConfigurationTypes", BindingFlags.Instance | BindingFlags.NonPublic)!;

    [Fact]
    public void RegisteredConfiguration_IsKept_AfterThePluginIsInitialized()
    {
        var manager = TestPluginManager.Create();
        var plugins = TestPluginManager.Plugins(manager);
        var active = PluginTestDoubles.InstalledPluginInfo(typeof(PluginTestDoubles.TestPlugin), Guid.NewGuid());
        plugins.Add(PluginTestDoubles.InactivePluginInfo(active));

        var services = new ServiceCollection();
        services.AddConfiguration<PluginTestDoubles.TestPlugin, RegisteredConfiguration>();
        _takeRegistrations.Invoke(manager, [plugins[0], services, Array.Empty<ServiceDescriptor>()]);

        // Initializing a plugin puts a new, active entry in its place.
        plugins[0] = active;

        var types = (List<(Type ConfigurationType, LocalPluginInfo PluginInfo)>)_getConfigurationTypes.Invoke(manager, [])!;

        var (_, pluginInfo) = Assert.Single(types, entry => entry.ConfigurationType == typeof(RegisteredConfiguration));
        Assert.Same(active, pluginInfo);
    }

    public sealed class RegisteredConfiguration : IConfiguration;
}
