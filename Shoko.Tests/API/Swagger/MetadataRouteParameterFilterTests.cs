using System.Linq;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.OpenApi;
using Shoko.Abstractions.Metadata;
using Shoko.Server.API.Swagger;
using Shoko.Server.API.v3.Controllers;
using Shoko.Tests.Infrastructure;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace Shoko.Tests.API.Swagger;

/// <summary>
/// Covers <see cref="MetadataRouteParameterFilter"/>, which publishes the
/// <c>{source}</c> and <c>{kind}</c> parameters as enums of the registered
/// values.
/// </summary>
public class MetadataRouteParameterFilterTests
{
    private static OpenApiOperation Apply()
    {
        _ = TestSources.Plugin;
        MetadataRouteParameterFilter.Capture();

        var description = new ApiDescription();
        description.ParameterDescriptions.Add(new() { Name = "source", Source = BindingSource.Path, Type = typeof(MetadataSource) });
        description.ParameterDescriptions.Add(new() { Name = "kind", Source = BindingSource.Path, Type = typeof(MetadataEntityType) });
        description.ParameterDescriptions.Add(new() { Name = "id", Source = BindingSource.Path, Type = typeof(string) });
        description.ParameterDescriptions.Add(new() { Name = "source", Source = BindingSource.Body, Type = typeof(MetadataSource) });
        var operation = new OpenApiOperation
        {
            Parameters =
            [
                new OpenApiParameter { Name = "source", In = ParameterLocation.Path, Schema = new OpenApiSchema { Type = JsonSchemaType.String } },
                new OpenApiParameter { Name = "kind", In = ParameterLocation.Path, Schema = new OpenApiSchema { Type = JsonSchemaType.String } },
                new OpenApiParameter { Name = "id", In = ParameterLocation.Path, Schema = new OpenApiSchema { Type = JsonSchemaType.String } },
            ],
        };
        var method = typeof(MetadataEntryController).GetMethod(nameof(MetadataEntryController.GetEntry))!;
        new MetadataRouteParameterFilter().Apply(operation, new OperationFilterContext(description, null!, new SchemaRepository(), new OpenApiDocument(), method));
        return operation;
    }

    private static string[] EnumOf(IOpenApiParameter parameter)
        => [.. parameter.Schema!.Enum!.Select(value => value!.GetValue<string>())];

    [Fact]
    public void TheSourceAndKindAreEnumsOfTheRegisteredValues()
    {
        var parameters = Apply().Parameters!;
        var (source, kind) = (parameters[0], parameters[1]);

        Assert.Equal(JsonSchemaType.String, source.Schema!.Type);
        Assert.Contains("anidb", EnumOf(source));
        Assert.Contains("tmdb", EnumOf(source));
        Assert.Contains("test-plugin", EnumOf(source));
        Assert.DoesNotContain("TMDB", EnumOf(source));
        Assert.Contains("series", EnumOf(kind));
        Assert.Contains("ordering", EnumOf(kind));
    }

    [Fact]
    public void OtherParametersAreLeftAlone()
        => Assert.Null(Apply().Parameters![2].Schema!.Enum);
}
