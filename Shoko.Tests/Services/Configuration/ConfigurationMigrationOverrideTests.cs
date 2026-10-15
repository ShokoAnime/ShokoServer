using System;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Namotion.Reflection;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Services.Configuration;
using Xunit;

namespace Shoko.Tests.Services.Configuration;

/// <summary>
///   Coverage for loading a configuration that needs a migration while an
///   environment variable overrides one of its values.
/// </summary>
[Collection(ConfigurationSchemaCollection.Name)]
public class ConfigurationMigrationOverrideTests : IDisposable
{
    private const string PortVariable = "SHOKO_TESTS_MIGRATED_PORT";

    private readonly string _path = Path.Join(Path.GetTempPath(), $"shoko-migration-override-{Guid.NewGuid():N}");

    /// <inheritdoc/>
    public void Dispose()
    {
        Environment.SetEnvironmentVariable(PortVariable, null);
        if (Directory.Exists(_path))
            Directory.Delete(_path, true);
    }

    [Fact]
    public void Load_MigratedWithAnOverride_KeepsTheFileValueOnDisk()
    {
        Directory.CreateDirectory(_path);
        var file = Path.Join(_path, "migrated.json");
        File.WriteAllText(file, """{"OldName":"Kept","Port":8111}""");
        Environment.SetEnvironmentVariable(PortVariable, "8211");
        var (service, info) = Create(file);

        var config = (MigratedConfiguration)service.Load(info);

        Assert.Equal(8211, config.Port);
        var saved = JObject.Parse(File.ReadAllText(file));
        Assert.Equal(8111, saved.Value<int>(nameof(MigratedConfiguration.Port)));
        Assert.Equal("Kept", saved.Value<string>(nameof(MigratedConfiguration.Name)));
    }

    private (ConfigurationService Service, ConfigurationInfo Info) Create(string file)
    {
        var applicationPaths = new Mock<IApplicationPaths>();
        applicationPaths.SetupGet(x => x.DataPath).Returns(_path);
        applicationPaths.SetupGet(x => x.ConfigurationsPath).Returns(_path);
        var service = new ConfigurationService(NullLoggerFactory.Instance, applicationPaths.Object, Mock.Of<IPluginManager>());
        var type = typeof(MigratedConfiguration);
        var info = new ConfigurationInfo(service)
        {
            ID = Guid.NewGuid(),
            Path = file,
            Name = "Migrated",
            Description = string.Empty,
            HasCustomActions = false,
            HasCustomNewFactory = false,
            HasCustomValidation = false,
            HasCustomSave = false,
            HasCustomLoad = false,
            HasLiveEdit = false,
            Type = type,
            ContextualType = type.ToContextualType(),
            Schema = service.GenerateSchema(type),
            PluginInfo = null!,
        };
        return (service, info);
    }

    /// <summary>A configuration that renames a property on load, with a port an environment variable sets.</summary>
    public sealed class MigratedConfiguration : IConfigurationWithMigrations
    {
        /// <summary>The name, once <c>OldName</c>.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>The port.</summary>
        [EnvironmentVariable(PortVariable)]
        public int Port { get; set; }

        /// <summary>Renames <c>OldName</c> to <c>Name</c>.</summary>
        /// <param name="config">The serialized configuration.</param>
        /// <param name="applicationPaths">The application paths.</param>
        /// <returns>The migrated configuration.</returns>
        public static string ApplyMigrations(string config, IApplicationPaths applicationPaths)
        {
            var document = JObject.Parse(config);
            if (document.Property("OldName") is { } oldName)
            {
                document["Name"] = oldName.Value;
                oldName.Remove();
            }

            return document.ToString();
        }
    }
}
