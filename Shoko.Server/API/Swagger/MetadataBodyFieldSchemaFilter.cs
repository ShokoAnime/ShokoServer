using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Microsoft.OpenApi;
using Newtonsoft.Json;
using Shoko.Abstractions.Metadata;
using Shoko.Server.API.Annotations;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Shoko.Server.API.Swagger;

/// <summary>
///   Publishes each request body field marked with
///   <see cref="RegisteredMetadataValuesAttribute"/> as a string enum of the
///   registered sources or entity types, or as an array of one.
/// </summary>
/// <remarks>
///   The values are the ones <see cref="MetadataRouteParameterFilter"/> took
///   once registration closed, so route, query and body fields agree.
/// </remarks>
public sealed class MetadataBodyFieldSchemaFilter : ISchemaFilter
{
    /// <summary>
    ///   Replaces the schema of each marked property of a body type.
    /// </summary>
    /// <param name="model">The schema of the type being described.</param>
    /// <param name="context">What the schema is described from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> or <paramref name="context"/> is <c>null</c>.</exception>
    public void Apply(IOpenApiSchema model, SchemaFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(context);

        if (model is not OpenApiSchema schema || schema.Properties is not { Count: > 0 } properties || context.Type is not { IsClass: true } type)
            return;

        var values = MetadataRouteParameterFilter.Values;
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetCustomAttribute<RegisteredMetadataValuesAttribute>() is null || ValueSchema(property.PropertyType, values) is not { } replacement)
                continue;

            var name = property.GetCustomAttribute<JsonPropertyAttribute>()?.PropertyName ?? property.Name;
            if (properties.Keys.FirstOrDefault(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase)) is not { } key)
                continue;

            if (properties[key] is OpenApiSchema existing && !string.IsNullOrEmpty(existing.Description))
                replacement.Description = existing.Description;
            properties[key] = replacement;
        }
    }

    /// <summary>
    ///   The schema of a field of the given type, when it takes one or more
    ///   sources or entity types.
    /// </summary>
    /// <param name="type">The field's type.</param>
    /// <param name="values">The registered values.</param>
    /// <returns>The schema, or <c>null</c> for any other type.</returns>
    internal static OpenApiSchema? ValueSchema(Type type, MetadataRouteParameterFilter.RegisteredValues values)
    {
        if (type == typeof(MetadataSource))
            return MetadataRouteParameterFilter.Create("metadata source", values.Sources);
        if (type == typeof(MetadataEntityType))
            return MetadataRouteParameterFilter.Create("metadata entity type", values.EntityTypes);

        if (type == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(type))
            return null;

        var elementType = type.IsArray
            ? type.GetElementType()
            : type.GetInterfaces().Append(type).FirstOrDefault(face => face.IsGenericType && face.GetGenericTypeDefinition() == typeof(System.Collections.Generic.IEnumerable<>))?.GetGenericArguments()[0];
        if (elementType is null || ValueSchema(elementType, values) is not { } items)
            return null;

        return new()
        {
            Type = JsonSchemaType.Array,
            Items = items,
            UniqueItems = type.GetInterfaces().Any(face => face.IsGenericType && face.GetGenericTypeDefinition() == typeof(System.Collections.Generic.ISet<>)) ? true : null,
        };
    }
}
