using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using NJsonSchema.Validation;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Services.Configuration;
using Xunit;

namespace Shoko.Tests.Services.Configuration;

/// <summary>
/// Unit tests for how <see cref="ShokoJsonSchemaGenerator"/> describes a <see cref="MetadataSource"/> in a
/// configuration schema: a plain string, allowed as a dictionary key, which
/// <see cref="ShokoJsonSchemaValidator{TConfig}"/> leaves as it was written.
/// </summary>
public class MetadataSourceSchemaTests
{
    public class SourceConfiguration : INewtonsoftJsonConfiguration
    {
        public MetadataSource Source { get; set; } = MetadataSource.AniDB;

        public List<MetadataSource> Order { get; set; } = [MetadataSource.AniDB, MetadataSource.TMDB];

        public Dictionary<MetadataSource, string> Names { get; set; } = [];
    }

    private static JsonSchema CreateSchema()
        => new ShokoJsonSchemaGenerator(new JsonSerializerSettings { Converters = [new StringEnumConverter()] }, new JsonSerializerOptions())
            .GetSchemaForType(typeof(SourceConfiguration))
            .Schema;

    private static JObject CreateSchemaJson()
        => JObject.Parse(CreateSchema().ToJson());

    private static (JToken Token, ICollection<ValidationError> Errors) Validate(string json)
        => new ShokoJsonSchemaValidator<SourceConfiguration>(NullLogger.Instance, null!, null!, null, saveValidation: false, loadValidation: true)
            .Validate(json, CreateSchema());

    private static void AssertIsPlainString(JToken? schema)
    {
        Assert.NotNull(schema);
        Assert.Equal("string", schema["type"]!.Value<string>());
        Assert.Null(schema["format"]);
        Assert.Null(schema["pattern"]);
        Assert.Null(schema["examples"]);
        Assert.Null(schema["properties"]);
    }

    [Fact]
    public void Schema_DescribesASourceAsAPlainStringAndAllowsItAsADictionaryKey()
    {
        var properties = CreateSchemaJson()["properties"]!;

        AssertIsPlainString(properties["Source"]);
        AssertIsPlainString(properties["Order"]!["items"]);
        Assert.Equal("object", properties["Names"]!["type"]!.Value<string>());
        Assert.Equal("string", properties["Names"]!["additionalProperties"]!["type"]!.Value<string>());
    }

    [Theory]
    [InlineData("anidb")]
    [InlineData("AniDB")]
    [InlineData("themoviedb")]
    [InlineData("some-other-source")]
    public void Validate_LeavesTheTextAsItWasWritten(string text)
    {
        var (token, errors) = Validate($$$"""{"Source": "{{{text}}}", "Order": ["{{{text}}}"], "Names": {"{{{text}}}": "a"}}""");

        Assert.Empty(errors);
        Assert.Equal(text, token["Source"]!.Value<string>());
        Assert.Equal(text, token["Order"]![0]!.Value<string>());
        Assert.Equal([text], ((JObject)token["Names"]!).Properties().Select(property => property.Name));
    }
}
