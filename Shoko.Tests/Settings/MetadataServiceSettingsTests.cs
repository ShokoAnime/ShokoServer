using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Services.Configuration;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Settings;

/// <summary>
/// Round-trips <see cref="MetadataServiceSettings"/> through the real
/// <see cref="ConfigurationService"/>, since the seeding tells "never decided"
/// from "decided: nobody" by whether an entry is missing or
/// <see langword="null"/>, and that only holds if the file keeps the
/// difference.
/// </summary>
/// <remarks>
/// The service is not handed the plugin's configuration types here, so it
/// files these under the core settings' path. Where the file lands is not
/// what is under test; what the serializer keeps is.
/// </remarks>
public sealed class MetadataServiceSettingsTests : IDisposable
{
    #region Fixture

    private static readonly Guid _tmdb = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly string _dataPath = Path.Join(Path.GetTempPath(), $"shoko-metadata-settings-tests-{Guid.NewGuid():N}");

    public MetadataServiceSettingsTests()
        => Directory.CreateDirectory(_dataPath);

    public void Dispose()
    {
        if (Directory.Exists(_dataPath))
            Directory.Delete(_dataPath, recursive: true);
    }

    /// <summary>
    /// A new service each time, so nothing is served from the one before's
    /// cache and every load is a read of the file.
    /// </summary>
    private ConfigurationService NewService()
    {
        var applicationPaths = new Mock<IApplicationPaths>(MockBehavior.Loose);
        applicationPaths.SetupGet(paths => paths.DataPath).Returns(_dataPath);
        applicationPaths.SetupGet(paths => paths.ConfigurationsPath).Returns(Path.Join(_dataPath, "configurations"));
        return new ConfigurationService(NullLoggerFactory.Instance, applicationPaths.Object, new Mock<IPluginManager>(MockBehavior.Loose).Object);
    }

    private MetadataServiceSettings SaveAndReload(Action<MetadataServiceSettings> configure)
    {
        var writer = NewService();
        var settings = writer.Load<MetadataServiceSettings>();
        configure(settings);
        writer.Save(settings);

        return NewService().Load<MetadataServiceSettings>();
    }

    #endregion

    [Fact]
    public void AnOrderWithNothingEnabled_SurvivesAsADecision_WhileAMissingOneStaysUndecided()
    {
        var reloaded = SaveAndReload(settings => settings.Sources[MetadataSource.TMDB] = new()
        {
            AutoLinker = null,
            Providers = { [MetadataEntityType.Series] = [new(_tmdb, true)], [MetadataEntityType.Movie] = [new(_tmdb, false)] },
        });

        var tmdb = reloaded.Sources[MetadataSource.TMDB];
        Assert.Null(tmdb.AutoLinker);
        Assert.Equal([new(_tmdb, true)], tmdb.Providers[MetadataEntityType.Series]);
        Assert.Equal([new(_tmdb, false)], tmdb.Providers[MetadataEntityType.Movie]);
        Assert.False(tmdb.Providers.ContainsKey(MetadataEntityType.Collection));
        Assert.False(reloaded.Sources.ContainsKey(TestSources.AniList));
    }

    [Fact]
    public void OlderAssignmentsAreReadButNeverWritten()
    {
        var writer = NewService();
        var settings = writer.Load<MetadataServiceSettings>();
        settings.Sources[MetadataSource.TMDB] = new() { Enabled = new() { [MetadataEntityType.Series] = _tmdb } };
        writer.Save(settings);
        var file = Directory.GetFiles(_dataPath, "*.json", SearchOption.AllDirectories).Single(path => File.ReadAllText(path).Contains("\"Providers\""));
        Assert.DoesNotContain("\"Enabled\"", File.ReadAllText(file));

        // A file from before the order kept one provider per type.
        var json = JObject.Parse(File.ReadAllText(file));
        var tmdb = (JObject)((JObject)json["Sources"]!).Properties().Single().Value;
        tmdb.Remove("Providers");
        tmdb["Enabled"] = new JObject { ["series"] = _tmdb.ToString(), ["movie"] = null };
        File.WriteAllText(file, json.ToString());

        var older = NewService().Load<MetadataServiceSettings>().Sources[MetadataSource.TMDB];
        Assert.Equal(_tmdb, older.Enabled![MetadataEntityType.Series]);
        Assert.Null(older.Enabled[MetadataEntityType.Movie]);
    }

    [Fact]
    public void EntityTypeKeys_AreWrittenAsValues()
    {
        var writer = NewService();
        var settings = writer.Load<MetadataServiceSettings>();
        settings.Sources[MetadataSource.TMDB] = new()
        {
            Providers = { [MetadataEntityType.Series] = [new(_tmdb, true)], [MetadataEntityType.Collection] = [] },
        };
        writer.Save(settings);
        var file = Directory.GetFiles(_dataPath, "*.json", SearchOption.AllDirectories).Single(path => File.ReadAllText(path).Contains("\"Providers\""));
        var json = File.ReadAllText(file);

        Assert.Contains("\"series\"", json);
        Assert.Contains("\"collection\"", json);
    }
}
