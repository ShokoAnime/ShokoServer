using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services.Configuration;

/// <summary>
/// Checks that the per-source image section of the server settings round-trips.
/// </summary>
public class MetadataImageSettingsSchemaTests
{
    [Fact]
    public void ThePerSourceSettingsRoundTrip()
    {
        var settings = new ServerSettings();
        settings.Image.MetadataSources.Add(new() { Source = TestSources.Plugin, MaxAutoPosters = 3, InternalImageLanguageOrder = ["ja"] });

        var json = JsonConvert.SerializeObject(settings, ServerSettings.SerializationSettings);
        var read = JsonConvert.DeserializeObject<ServerSettings>(json, new JsonSerializerSettings
        {
            Converters = [new StringEnumConverter()],
            ObjectCreationHandling = ObjectCreationHandling.Replace,
        })!;

        var own = Assert.Single(read.Image.MetadataSources);
        Assert.Equal(TestSources.Plugin, own.Source);
        Assert.Equal(3, own.MaxAutoPosters);
        Assert.Equal([Abstractions.Metadata.Enums.TitleLanguage.Japanese], own.ImageLanguageOrder);
    }
}
