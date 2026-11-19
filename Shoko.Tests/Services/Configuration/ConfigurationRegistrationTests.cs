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
///   Coverage for configuration types tied to the plugin they are given with,
///   and for configurations sharing a file.
/// </summary>
[Collection(ConfigurationSchemaCollection.Name)]
public class ConfigurationRegistrationTests : IDisposable
{
    private readonly string _path = Path.Join(Path.GetTempPath(), $"shoko-configuration-registration-{Guid.NewGuid():N}");

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(_path))
            Directory.Delete(_path, true);
    }

    private ConfigurationService CreateService()
    {
        var applicationPaths = new Mock<IApplicationPaths>();
        applicationPaths.SetupGet(x => x.DataPath).Returns(_path);
        applicationPaths.SetupGet(x => x.ConfigurationsPath).Returns(Path.Join(_path, "configurations"));
        var core = PluginTestDoubles.InstalledPluginInfo(typeof(CorePlugin), Guid.NewGuid());
        var pluginManager = new Mock<IPluginManager>();
        pluginManager.Setup(x => x.GetPluginInfo(It.IsAny<Assembly>())).Returns(core);
        pluginManager.Setup(x => x.GetPluginInfo<CorePlugin>()).Returns(core);
        var service = new ConfigurationService(NullLoggerFactory.Instance, applicationPaths.Object, pluginManager.Object);

        // A real start loads the server settings before the plugins add their parts.
        service.Load<ServerSettings>();
        return service;
    }

    [Fact]
    public void AddParts_TiesATypeToTheGivenPlugin()
    {
        var service = CreateService();
        var pluginInfo = PluginTestDoubles.InstalledPluginInfo(typeof(PluginWithConfiguration), Guid.NewGuid());

        service.AddParts([(typeof(MainConfiguration), pluginInfo)], (_, ex) => throw ex);

        var info = service.GetConfigurationInfo<MainConfiguration>();
        Assert.Same(pluginInfo, info.PluginInfo);
        Assert.Same(info, pluginInfo.ConfigurationInfo);
        Assert.StartsWith(Path.Join(_path, "configurations"), info.Path);
    }

    [Fact]
    public void AddParts_FailsEveryPluginSharingAFile()
    {
        var service = CreateService();
        var first = PluginTestDoubles.InstalledPluginInfo(typeof(PluginWithConfiguration), Guid.NewGuid());
        var second = PluginTestDoubles.InstalledPluginInfo(typeof(PluginWithConfiguration), Guid.NewGuid());
        var failed = new System.Collections.Generic.List<(LocalPluginInfo, Exception)>();

        service.AddParts([(typeof(SharedConfiguration), first), (typeof(OtherSharedConfiguration), second), (typeof(MainConfiguration), second)], (plugin, ex) => failed.Add((plugin, ex)));

        Assert.Equivalent(new[] { first, second }, failed.ConvertAll(entry => entry.Item1));
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
