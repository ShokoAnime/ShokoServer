using System;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Shoko.Server.API.Swagger;

/// <summary>
///   Marks a request body nullable when its parameter is declared nullable
///   or takes an empty body, since the body's schema is the type's own.
/// </summary>
public sealed class NullableRequestBodyFilter : IRequestBodyFilter
{
    private static readonly NullabilityInfoContext _nullability = new();

    public void Apply(IOpenApiRequestBody requestBody, RequestBodyFilterContext context)
    {
        if (requestBody is not OpenApiRequestBody { Content: { } content } body
            || context.BodyParameterDescription?.ParameterDescriptor is not IParameterInfoParameterDescriptor { ParameterInfo: var parameter })
            return;

        var allowsEmpty = parameter.GetCustomAttribute<FromBodyAttribute>()?.EmptyBodyBehavior is EmptyBodyBehavior.Allow;
        if (!allowsEmpty && !IsNullable(parameter))
            return;

        if (allowsEmpty)
            body.Required = false;
        foreach (var mediaType in content.Values)
        {
            mediaType.Schema = mediaType.Schema switch
            {
                OpenApiSchema schema => WithNull(schema),
                OpenApiSchemaReference reference => new OpenApiSchema { AllOf = [reference], Type = JsonSchemaType.Null },
                var other => other,
            };
        }
    }

    private static bool IsNullable(ParameterInfo parameter)
        => parameter.ParameterType.IsValueType
            ? Nullable.GetUnderlyingType(parameter.ParameterType) is not null
            : _nullability.Create(parameter).WriteState is NullabilityState.Nullable;

    private static OpenApiSchema WithNull(OpenApiSchema schema)
    {
        schema.Type = (schema.Type ?? 0) | JsonSchemaType.Null;
        return schema;
    }
}
