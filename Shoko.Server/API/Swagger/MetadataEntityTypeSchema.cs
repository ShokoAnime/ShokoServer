using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Shoko.Abstractions.Metadata;
using Shoko.Server.API.Converters;

namespace Shoko.Server.API.Swagger;

/// <summary>
///   The OpenAPI schema for a <see cref="MetadataEntityType"/>, which the API
///   sends as a string rather than an object.
/// </summary>
public static class MetadataEntityTypeSchema
{
    /// <summary>
    ///   Creates the schema: a string holding the entity type as the old enum
    ///   spelled it, or its value when the old enum lacked it.
    /// </summary>
    /// <returns>A new schema.</returns>
    public static OpenApiSchema Create()
        => new()
        {
            Type = JsonSchemaType.String,
            Description = "A metadata entity type, sent as the old DataEntityType enum spelled it (e.g. Show), or as its value (e.g. ordering) when the enum had no such kind. A registered entity type's value, an alias or an old spelling is accepted as input, ignoring case and reading _ as -.",
            Example = JsonValue.Create(LegacyMetadataSpellings.Of(MetadataEntityType.Series)),
        };
}
