using System.Collections.Generic;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.OpenApi;
using Moq;
using Shoko.Server.API.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace Shoko.Tests.API.Swagger;

/// <summary>
///   A request body is nullable in the documents as its parameter is in C#.
/// </summary>
public class NullableRequestBodyFilterTests
{
    [Theory]
    [InlineData(nameof(Nullable), true, true)]
    [InlineData(nameof(AllowsEmpty), true, false)]
    [InlineData(nameof(Required), false, true)]
    public void Apply_MarksTheBodyAsItsParameterIsDeclared(string method, bool nullable, bool required)
    {
        var body = new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType> { ["application/json"] = new() { Schema = new OpenApiSchemaReference("Body") } },
        };

        new NullableRequestBodyFilter().Apply(body, CreateContext(method));

        var schema = body.Content["application/json"].Schema;
        Assert.Equal(nullable, schema is OpenApiSchema { Type: JsonSchemaType.Null, AllOf.Count: 1 });
        Assert.Equal(required, body.Required);
    }

    private static RequestBodyFilterContext CreateContext(string method)
    {
        var parameter = typeof(NullableRequestBodyFilterTests).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!.GetParameters()[0];
        var description = new ApiParameterDescription
        {
            Name = parameter.Name!,
            Source = BindingSource.Body,
            ParameterDescriptor = new ControllerParameterDescriptor { Name = parameter.Name!, ParameterType = parameter.ParameterType, ParameterInfo = parameter },
        };
        return new RequestBodyFilterContext(description, [], Mock.Of<ISchemaGenerator>(), new SchemaRepository(), new OpenApiDocument());
    }

    private static void Nullable(Payload? body) { }

    private static void AllowsEmpty([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] Payload body) { }

    private static void Required(Payload body) { }

    private sealed class Payload;
}
