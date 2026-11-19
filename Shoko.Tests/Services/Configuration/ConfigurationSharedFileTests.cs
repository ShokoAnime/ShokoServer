using System;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Server.Plugin;
using Shoko.Server.Services.Configuration;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services.Configuration;

/// <summary>
///   Coverage for configurations sharing a file.
/// </summary>
[Collection(ConfigurationSchemaCollection.Name)]
public class ConfigurationSharedFileTests : IDisposable
{
    private readonly string _path = Path.Join(Path.GetTempPath(), $"shoko-configuration-shared-file-{Guid.NewGuid():N}");

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(_path))
            Directory.Delete(_path, true);
    }

    private ConfigurationService CreateService(LocalPluginInfo pluginInfo)
    {
        var applicationPaths = new Mock<IApplicationPaths>();
        applicationPaths.SetupGet(x => x.DataPath).Returns(_path);
        applicationPaths.SetupGet(x => x.ConfigurationsPath).Returns(Path.Join(_path, "configurations"));
        var pluginManager = new Mock<IPluginManager>();
        pluginManager.Setup(x => x.GetPluginInfo(It.IsAny<Assembly>())).Returns(pluginInfo);
        pluginManager.Setup(x => x.GetPluginInfo<CorePlugin>()).Returns(pluginInfo);
        var service = new ConfigurationService(NullLoggerFactory.Instance, applicationPaths.Object, pluginManager.Object);

        // A real start loads the server settings before the plugins add their parts.
        service.Load<ServerSettings>();
        return service;
    }

    [Fact]
    public void AddParts_LeavesOutConfigurationsSharingAFile()
    {
        var pluginInfo = PluginTestDoubles.InstalledPluginInfo(typeof(PluginWithConfiguration), Guid.NewGuid());
        var service = CreateService(pluginInfo);
        var failed = new System.Collections.Generic.List<(LocalPluginInfo, Exception)>();

        service.AddParts([typeof(SharedConfiguration), typeof(OtherSharedConfiguration), typeof(MainConfiguration)], (plugin, ex) => failed.Add((plugin, ex)));

        Assert.Equal(2, failed.Count);
        Assert.All(failed, entry => Assert.Same(pluginInfo, entry.Item1));
        Assert.All(failed, entry => Assert.Contains("shared.json", entry.Item2.Message, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(service.GetAllConfigurationInfos(), info => info.Type == typeof(SharedConfiguration) || info.Type == typeof(OtherSharedConfiguration));
        Assert.Contains(service.GetAllConfigurationInfos(), info => info.Type == typeof(MainConfiguration));
    }

    /// <summary>A plugin declaring <see cref="MainConfiguration"/> as its main configuration.</summary>
    public sealed class PluginWithConfiguration : IPlugin<MainConfiguration>
    {
        /// <inheritdoc/>
        public Guid ID => Guid.Empty;

        /// <inheritdoc/>
        public string Name => "With Configuration";
    }

    /// <summary>The main configuration.</summary>
    public sealed class MainConfiguration : IPluginConfiguration
    {
        /// <summary>A value.</summary>
        public int Value { get; set; }
    }

    /// <summary>A configuration stored next to the server settings.</summary>
    [StorageLocation(RelativePath = "shared")]
    public sealed class SharedConfiguration : IPluginConfiguration
    {
        /// <summary>A value.</summary>
        public int Value { get; set; }
    }

    /// <summary>Another configuration pointing at the same file.</summary>
    [StorageLocation(RelativePath = "Shared.json")]
    public sealed class OtherSharedConfiguration : IPluginConfiguration
    {
        /// <summary>A value.</summary>
        public int Value { get; set; }
    }
}
