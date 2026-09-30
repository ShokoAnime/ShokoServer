using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Shoko.Abstractions.Metadata;
using Shoko.Server.API.Converters;

namespace Shoko.Server.API.Swagger;

/// <summary>
///   The OpenAPI schema for a <see cref="MetadataSource"/>, which the API
///   sends as a string rather than an object.
/// </summary>
public static class MetadataSourceSchema
{
    /// <summary>
    ///   Creates the schema: a string holding the source as the old enum
    ///   spelled it, or its value when the old enum lacked it.
    /// </summary>
    /// <returns>A new schema.</returns>
    public static OpenApiSchema Create()
        => new()
        {
            Type = JsonSchemaType.String,
            Description = "A metadata source, sent as the old DataSource enum spelled it (e.g. AniDB), or as its value (e.g. a plugin's own source) when the enum had no such source. A registered source's value, an alias or an old spelling is accepted as input, ignoring case and reading _ as -.",
            Example = JsonValue.Create(LegacyMetadataSpellings.Of(MetadataSource.AniDB)),
        };
}
