using System;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Server.Plugin;
using Shoko.Server.Services.Configuration;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services.Configuration;

/// <summary>
///   Coverage for a plugin's main configuration, declared through
///   <see cref="IPlugin{TConfiguration}"/>, being attached to its plugin info.
/// </summary>
[Collection(ConfigurationSchemaCollection.Name)]
public class PluginMainConfigurationTests
{
    [Fact]
    public void AddParts_AttachesTheDeclaredConfiguration()
    {
        var pluginInfo = AddParts(typeof(PluginWithConfiguration), typeof(MainConfiguration), typeof(OtherConfiguration));

        Assert.Equal(typeof(MainConfiguration), pluginInfo.ConfigurationInfo?.Type);
    }

    [Fact]
    public void AddParts_LeavesAPluginWithoutOneEmpty()
    {
        var pluginInfo = AddParts(typeof(PluginWithoutConfiguration), typeof(MainConfiguration), typeof(OtherConfiguration));

        Assert.Null(pluginInfo.ConfigurationInfo);
    }

    private static LocalPluginInfo AddParts(Type pluginType, params Type[] configurationTypes)
    {
        var path = Path.GetTempPath();
        var applicationPaths = new Mock<IApplicationPaths>();
        applicationPaths.SetupGet(x => x.DataPath).Returns(path);
        applicationPaths.SetupGet(x => x.ConfigurationsPath).Returns(path);
        var pluginInfo = PluginTestDoubles.InstalledPluginInfo(pluginType, Guid.NewGuid());
        var pluginManager = new Mock<IPluginManager>();
        pluginManager.Setup(x => x.GetPluginInfo(It.IsAny<Assembly>())).Returns(pluginInfo);
        pluginManager.Setup(x => x.GetPluginInfo<CorePlugin>()).Returns(pluginInfo);
        var service = new ConfigurationService(NullLoggerFactory.Instance, applicationPaths.Object, pluginManager.Object);

        service.AddParts(configurationTypes, (_, ex) => throw ex);

        return pluginInfo;
    }

    /// <summary>A plugin declaring <see cref="MainConfiguration"/> as its main configuration.</summary>
    public sealed class PluginWithConfiguration : IPlugin<MainConfiguration>
    {
        /// <inheritdoc/>
        public Guid ID => Guid.Empty;

        /// <inheritdoc/>
        public string Name => "With Configuration";
    }

    /// <summary>A plugin declaring no main configuration.</summary>
    public sealed class PluginWithoutConfiguration : IPlugin
    {
        /// <inheritdoc/>
        public Guid ID => Guid.Empty;

        /// <inheritdoc/>
        public string Name => "Without Configuration";
    }

    /// <summary>The main configuration.</summary>
    public sealed class MainConfiguration : IPluginConfiguration
    {
        /// <summary>A value.</summary>
        public int Value { get; set; }
    }

    /// <summary>Another configuration of the same plugin.</summary>
    public sealed class OtherConfiguration : IPluginConfiguration
    {
        /// <summary>A value.</summary>
        public int Value { get; set; }
    }
}
