using System.Text.Json;
using Newtonsoft.Json;
using Shoko.Server.Services.Configuration;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.Services.Configuration;

/// <summary>
/// Unit tests for the descriptions <see cref="ShokoJsonSchemaGenerator"/> reads from the XML docs.
/// </summary>
public class SchemaDescriptionTests
{
    [Fact]
    public void Description_WritesALanguageKeywordAsInlineCode()
    {
        var schema = new ShokoJsonSchemaGenerator(new JsonSerializerSettings(), new JsonSerializerOptions())
            .GetSchemaForType(typeof(MetadataSourceSettings))
            .Schema;

        // Documented as "<see langword="null"/> is decided as nobody."
        Assert.Contains("never written back. `null` is decided as nobody.", schema.Properties[nameof(MetadataSourceSettings.Enabled)].Description);
    }
}
