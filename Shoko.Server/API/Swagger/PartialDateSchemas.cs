using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Shoko.Abstractions.Metadata;

namespace Shoko.Server.API.Swagger;

/// <summary>
///   The OpenAPI schemas for a <see cref="PartialDateOnly"/> and a
///   <see cref="FuzzyDateOnly"/>, which the API sends as ISO 8601 text rather
///   than an object.
/// </summary>
public static class PartialDateSchemas
{
    /// <summary>
    ///   Creates the schema for a <see cref="PartialDateOnly"/>: a string
    ///   holding <c>yyyy</c>, <c>yyyy-MM</c> or <c>yyyy-MM-dd</c>.
    /// </summary>
    /// <returns>A new schema.</returns>
    public static OpenApiSchema CreatePartialDateOnly()
        => new()
        {
            Type = JsonSchemaType.String,
            Description = "A date where the month and the day may be unknown, as yyyy, yyyy-MM or yyyy-MM-dd.",
            Example = JsonValue.Create("2024-03"),
        };

    /// <summary>
    ///   Creates the schema for a <see cref="FuzzyDateOnly"/>: a string
    ///   holding <c>yyyy</c>, <c>yyyy-MM</c>, <c>yyyy-MM-dd</c>, <c>--MM</c>
    ///   or <c>--MM-dd</c>.
    /// </summary>
    /// <returns>A new schema.</returns>
    public static OpenApiSchema CreateFuzzyDateOnly()
        => new()
        {
            Type = JsonSchemaType.String,
            Description = "A date where the year, the month and the day may each be unknown, as yyyy, yyyy-MM, yyyy-MM-dd, --MM or --MM-dd.",
            Example = JsonValue.Create("--07-04"),
        };
}
