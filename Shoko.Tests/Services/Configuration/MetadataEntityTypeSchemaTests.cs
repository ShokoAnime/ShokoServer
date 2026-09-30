using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Services.Configuration;
using Xunit;

namespace Shoko.Tests.Services.Configuration;

/// <summary>
/// Unit tests for how <see cref="ShokoJsonSchemaGenerator"/> describes a
/// <see cref="MetadataEntityType"/> in a configuration schema: a plain string,
/// allowed as a dictionary key, which <see cref="ShokoJsonSchemaValidator{TConfig}"/>
/// leaves as it was written.
/// </summary>
public class MetadataEntityTypeSchemaTests
{
    public class EntityTypeConfiguration : INewtonsoftJsonConfiguration
    {
        public MetadataEntityType EntityType { get; set; } = MetadataEntityType.Series;

        public List<MetadataEntityType> Order { get; set; } = [MetadataEntityType.Series, MetadataEntityType.Movie];

        public Dictionary<MetadataEntityType, string> Names { get; set; } = [];
    }

    private static JsonSchema CreateSchema()
        => new ShokoJsonSchemaGenerator(new JsonSerializerSettings { Converters = [new StringEnumConverter()] }, new JsonSerializerOptions())
            .GetSchemaForType(typeof(EntityTypeConfiguration))
            .Schema;

    private static void AssertIsPlainString(JToken? schema)
    {
        Assert.NotNull(schema);
        Assert.Equal("string", schema["type"]!.Value<string>());
        Assert.Null(schema["format"]);
        Assert.Null(schema["pattern"]);
        Assert.Null(schema["properties"]);
    }

    [Fact]
    public void Schema_DescribesAKindAsAPlainStringAndAllowsItAsADictionaryKey()
    {
        var properties = JObject.Parse(CreateSchema().ToJson())["properties"]!;

        AssertIsPlainString(properties["EntityType"]);
        AssertIsPlainString(properties["Order"]!["items"]);
        Assert.Equal("object", properties["Names"]!["type"]!.Value<string>());
        Assert.Equal("string", properties["Names"]!["additionalProperties"]!["type"]!.Value<string>());
    }

    [Theory]
    [InlineData("series")]
    [InlineData("Show")]
    [InlineData("some-plugin-kind")]
    public void Validate_LeavesTheTextAsItWasWritten(string text)
    {
        var (token, errors) = new ShokoJsonSchemaValidator<EntityTypeConfiguration>(NullLogger.Instance, null!, null!, null, saveValidation: false, loadValidation: true)
            .Validate($$$"""{"EntityType": "{{{text}}}", "Order": ["{{{text}}}"], "Names": {"{{{text}}}": "a"}}""", CreateSchema());

        Assert.Empty(errors);
        Assert.Equal(text, token["EntityType"]!.Value<string>());
        Assert.Equal(text, token["Order"]![0]!.Value<string>());
        Assert.Equal([text], ((JObject)token["Names"]!).Properties().Select(property => property.Name));
    }
}
