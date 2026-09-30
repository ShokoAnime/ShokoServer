using System;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Exceptions;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Services.Configuration;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.Settings;

/// <summary>
/// Drives the real <see cref="ConfigurationService"/> over the real
/// <see cref="ServerSettings"/> to show that a value the schema lets through
/// but the type can not be read from, such as an unknown metadata source, is
/// reported as a validation error and never written.
/// </summary>
public sealed class ConfigurationUnreadableValueTests : IDisposable
{
    #region Fixture

    private readonly string _dataPath = Path.Join(Path.GetTempPath(), $"shoko-unreadable-value-tests-{Guid.NewGuid():N}");

    private readonly ConfigurationService _service;

    private readonly ConfigurationInfo _info;

    public ConfigurationUnreadableValueTests()
    {
        Directory.CreateDirectory(_dataPath);

        var applicationPaths = new Mock<IApplicationPaths>(MockBehavior.Loose);
        applicationPaths.SetupGet(paths => paths.DataPath).Returns(_dataPath);
        applicationPaths.SetupGet(paths => paths.ConfigurationsPath).Returns(Path.Join(_dataPath, "configurations"));

        _service = new ConfigurationService(NullLoggerFactory.Instance, applicationPaths.Object, new Mock<IPluginManager>(MockBehavior.Loose).Object);
        _info = _service.GetConfigurationInfo<ServerSettings>();
        _service.Load<ServerSettings>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_dataPath))
            Directory.Delete(_dataPath, recursive: true);
    }

    private string WithFirstTitleSource(string text)
    {
        var json = JObject.Parse(_service.Serialize(_service.Load<ServerSettings>()));
        json["Language"]!["SeriesTitleSourceOrder"]![0] = text;
        return json.ToString();
    }

    #endregion

    #region Tests

    [Theory]
    [InlineData("Locally Generated")]
    [InlineData("fanart.tv")]
    public void Validate_ReportsAnUnreadableSourceAtItsPath(string text)
    {
        var errors = _service.Validate(_info, WithFirstTitleSource(text));

        var (path, messages) = Assert.Single(errors);
        Assert.Equal("Language.SeriesTitleSourceOrder[0]", path);
        Assert.Contains(text, Assert.Single(messages));
    }

    [Fact]
    public void Save_RejectsAnUnreadableSourceAndLeavesTheFileAsItWas()
    {
        var before = File.ReadAllText(_info.Path!);

        Assert.Throws<ConfigurationValidationException>(() => _service.Save(_info, WithFirstTitleSource("Locally Generated")));

        Assert.Equal(before, File.ReadAllText(_info.Path!));
    }

    [Fact]
    public void Validate_AcceptsARegisteredSourceByAnyCase()
    {
        var errors = _service.Validate(_info, WithFirstTitleSource("TMDB"));

        Assert.Empty(errors);
    }

    #endregion
}
