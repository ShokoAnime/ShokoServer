using System.Collections.Generic;
using System.Linq;
using Microsoft.OpenApi;
using Shoko.Server.API.Swagger;
using Shoko.Server.API.v3.Models.Metadata.Input;
using Shoko.Tests.Infrastructure;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace Shoko.Tests.API.Swagger;

/// <summary>
/// Covers <see cref="MetadataBodyFieldSchemaFilter"/>, which publishes the
/// marked request body fields as enums of the registered values.
/// </summary>
public class MetadataBodyFieldSchemaFilterTests
{
    [Fact]
    public void AMarkedSetOfKindsIsAnArrayOfTheRegisteredValues()
    {
        _ = TestSources.Plugin;
        MetadataRouteParameterFilter.Capture();
        var schema = new OpenApiSchema
        {
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["EnabledEntityTypes"] = new OpenApiSchema { Type = JsonSchemaType.Array, Description = "The kinds." },
                ["AutoLink"] = new OpenApiSchema { Type = JsonSchemaType.Boolean },
            },
        };

        new MetadataBodyFieldSchemaFilter().Apply(schema, new SchemaFilterContext(typeof(MetadataProviderUpdateBody), null!, new SchemaRepository()));

        var field = Assert.IsType<OpenApiSchema>(schema.Properties["EnabledEntityTypes"]);
        Assert.Equal(JsonSchemaType.Array, field.Type);
        Assert.True(field.UniqueItems);
        Assert.Equal("The kinds.", field.Description);
        var values = field.Items!.Enum!.Select(value => value!.GetValue<string>()).ToList();
        Assert.Contains("series", values);
        Assert.Contains("movie", values);
        Assert.Null(schema.Properties["AutoLink"].Enum);
    }
}
