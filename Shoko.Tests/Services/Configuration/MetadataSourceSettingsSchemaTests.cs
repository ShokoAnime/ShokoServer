using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services.Configuration;

/// <summary>
/// Checks that the per-source metadata section of the server settings round-trips.
/// </summary>
public class MetadataSourceSettingsSchemaTests
{
    [Fact]
    public void ThePerSourceSettingsRoundTrip()
    {
        var settings = new ServerSettings();
        settings.Metadata.Sources.Add(new() { Source = TestSources.Plugin, Images = new() { MaxAutoPosters = 3, InternalImageLanguageOrder = ["ja"] } });
        settings.Metadata.Sources.Add(new() { Source = TestSources.AniList, EpisodeMatchLookAheadDays = 2 });

        var json = JsonConvert.SerializeObject(settings, ServerSettings.SerializationSettings);
        var read = JsonConvert.DeserializeObject<ServerSettings>(json, new JsonSerializerSettings
        {
            Converters = [new StringEnumConverter()],
            ObjectCreationHandling = ObjectCreationHandling.Replace,
        })!;

        Assert.Equal(2, read.Metadata.Sources.Count);
        var own = read.Metadata.GetImageSettings(TestSources.Plugin);
        Assert.Equal(3, own.MaxAutoPosters);
        Assert.Equal([Abstractions.Metadata.Enums.TitleLanguage.Japanese], own.ImageLanguageOrder);
        Assert.Null(read.Metadata.Sources[1].Images);
        Assert.Equal(2, read.Metadata.GetEpisodeMatchLookAheadDays(TestSources.AniList));
    }
}
