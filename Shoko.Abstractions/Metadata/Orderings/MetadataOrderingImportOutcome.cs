using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Orderings;

/// <summary>
///   What an ordering import did with one ordering of the payload.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum MetadataOrderingImportOutcome
{
    /// <summary>
    ///   A new local ordering was made, or would be on a dry run.
    /// </summary>
    Created = 0,

    /// <summary>
    ///   An existing local ordering was replaced, or would be on a dry run.
    /// </summary>
    Replaced = 1,

    /// <summary>
    ///   Nothing was written; the reason says why.
    /// </summary>
    Skipped = 2,
}
