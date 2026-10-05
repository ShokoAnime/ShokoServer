using System;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Services.Configuration;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.Settings;

/// <summary>
/// Loads a settings file written by 5.0.0, the oldest version upgrades are
/// supported from, through the real <see cref="ConfigurationService"/>, which
/// runs it through every later migration and then validates it against the
/// current schema, as the server does on start.
/// </summary>
public sealed class SettingsUpgradeTests : IDisposable
{
    private readonly string _dataPath = Path.Join(Path.GetTempPath(), $"shoko-settings-upgrade-tests-{Guid.NewGuid():N}");

    public SettingsUpgradeTests()
        => Directory.CreateDirectory(_dataPath);

    public void Dispose()
    {
        if (Directory.Exists(_dataPath))
            Directory.Delete(_dataPath, recursive: true);
    }

    [Fact]
    public void AFullyPopulatedV5File_LoadsAtTheLatestVersion()
    {
        var applicationPaths = new Mock<IApplicationPaths>(MockBehavior.Loose);
        applicationPaths.SetupGet(paths => paths.DataPath).Returns(_dataPath);
        applicationPaths.SetupGet(paths => paths.ConfigurationsPath).Returns(Path.Join(_dataPath, "configurations"));
        var service = new ConfigurationService(NullLoggerFactory.Instance, applicationPaths.Object, new Mock<IPluginManager>(MockBehavior.Loose).Object);
        var path = service.GetConfigurationInfo<ServerSettings>().Path!;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, TestData.TestData.ServerSettingsV5.Value);

        var settings = service.Load<ServerSettings>();

        Assert.Equal(SettingsMigrations.Version, settings.SettingsVersion);
    }
}
