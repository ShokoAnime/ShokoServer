using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.UI;
using Shoko.Server.API.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
///   The API spec names an enum's values as the wire sends them.
/// </summary>
public class EnumSchemaFilterTests
{
    private enum Sample
    {
        [EnumMember(Value = "section-container")]
        SectionContainer,

        Plain,
    }

    [Fact]
    public void WireNames_TakeTheEnumMemberValue_ElseTheName()
        => Assert.Equal(["section-container", "Plain"], EnumSchemaFilter<Sample>.GetWireNames());

    [Fact]
    public void WireNames_MatchWhatTheApiSends()
    {
        var sent = Enum.GetValues<UiElementKind>()
            .Select(kind => JsonConvert.DeserializeObject<string>(JsonConvert.SerializeObject(kind, new StringEnumConverter()))!)
            .Order();

        Assert.Equal(sent, EnumSchemaFilter<UiElementKind>.GetWireNames().Order());
    }

    [Fact]
    public void Apply_RewritesTheEnumsOwnSchema()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32", Enum = [JsonValue.Create(0), JsonValue.Create(1)] };

        new EnumSchemaFilter<Sample>().Apply(schema, CreateContext());

        Assert.Equal(JsonSchemaType.String, schema.Type);
        Assert.Null(schema.Format);
        Assert.Equal(["section-container", "Plain"], schema.Enum!.Select(value => value!.GetValue<string>()));
    }

    [Fact]
    public void Apply_LeavesAPropertysAllOfWrapperAlone()
    {
        var schema = new OpenApiSchema { AllOf = [new OpenApiSchemaReference("Sample")], Type = JsonSchemaType.Null, Description = "A sample." };

        new EnumSchemaFilter<Sample>().Apply(schema, CreateContext());

        Assert.Null(schema.Enum);
        Assert.Equal(JsonSchemaType.Null, schema.Type);
        Assert.Equal("A sample.", schema.Description);
    }

    [Theory]
    [InlineData("plain", "Plain")]
    [InlineData("SECTION-CONTAINER", "section-container")]
    [InlineData("SectionContainer", "section-container")]
    public void Apply_NamesAPropertysStringDefaultAsTheWireDoes(string written, string expected)
    {
        var schema = new OpenApiSchema { AllOf = [new OpenApiSchemaReference("Sample")], Default = JsonValue.Create(written) };

        new EnumSchemaFilter<Sample>().Apply(schema, CreateContext());

        Assert.Equal(expected, schema.Default!.GetValue<string>());
    }

    [Fact]
    public void Apply_NamesAPropertysNumberDefaultAsTheWireDoes()
    {
        var schema = new OpenApiSchema { AllOf = [new OpenApiSchemaReference("Sample")], Default = JsonValue.Create(1) };

        new EnumSchemaFilter<Sample>().Apply(schema, CreateContext());

        Assert.Equal("Plain", schema.Default!.GetValue<string>());
    }

    private static SchemaFilterContext CreateContext()
        => new(typeof(Sample), Mock.Of<ISchemaGenerator>(), new SchemaRepository());
}
