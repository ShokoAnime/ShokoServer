using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Services;
using Shoko.Server.Services.Configuration;
using Shoko.Server.Settings;

namespace Shoko.Tests.Infrastructure;

/// <summary>
///   Real <see cref="MetadataProviderManager"/>s over a settings file in a
///   folder of the test's, for tests that need what it decides.
/// </summary>
public static class TestProviderManagers
{
    /// <summary>
    ///   A manager standing in for one start of the server: a new boot on the
    ///   same folder reads back what an earlier one saved, as a restart would.
    /// </summary>
    /// <param name="dataPath">The folder holding the settings.</param>
    /// <returns>The manager, with no providers added yet.</returns>
    public static MetadataProviderManager Boot(string dataPath)
    {
        var applicationPaths = new Mock<IApplicationPaths>(MockBehavior.Loose);
        applicationPaths.SetupGet(paths => paths.DataPath).Returns(dataPath);
        applicationPaths.SetupGet(paths => paths.ConfigurationsPath).Returns(Path.Join(dataPath, "configurations"));
        var pluginManager = new Mock<IPluginManager>(MockBehavior.Loose);
        var pluginInfo = PluginTestDoubles.InstalledPluginInfo(typeof(PluginTestDoubles.TestPlugin), Guid.Parse("55555555-5555-5555-5555-555555555555"));
        pluginManager.Setup(manager => manager.GetPluginInfo(It.IsAny<Assembly>())).Returns(pluginInfo);

        var real = new ConfigurationService(NullLoggerFactory.Instance, applicationPaths.Object, new Mock<IPluginManager>(MockBehavior.Loose).Object);
        var configurationInfo = (ConfigurationInfo)RuntimeHelpers.GetUninitializedObject(typeof(ConfigurationInfo));
        var configurationService = new Mock<IConfigurationService>();
        configurationService.Setup(service => service.GetConfigurationInfo<MetadataServiceSettings>()).Returns(configurationInfo);
        configurationService.Setup(service => service.Load(It.IsAny<ConfigurationInfo>(), It.IsAny<bool>()))
            .Returns(() => real.Load<MetadataServiceSettings>());
        configurationService.Setup(service => service.Save(It.IsAny<MetadataServiceSettings>()))
            .Returns((MetadataServiceSettings settings) => real.Save(settings));
        return new(
            applicationPaths.Object,
            pluginManager.Object,
            configurationService.Object,
            new ConfigurationProvider<MetadataServiceSettings>(configurationService.Object),
            NullLogger<MetadataProviderManager>.Instance
        );
    }
}
