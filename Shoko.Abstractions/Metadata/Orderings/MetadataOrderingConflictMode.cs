using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Orderings;

/// <summary>
///   What an ordering import does when the series already has a local
///   ordering with the same name, compared without regard to case.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum MetadataOrderingConflictMode
{
    /// <summary>
    ///   Leave the existing ordering alone and skip the imported one.
    /// </summary>
    Skip = 0,

    /// <summary>
    ///   Replace the existing ordering whole, groups and images, keeping its
    ///   ID and whether it is the series' chosen ordering.
    /// </summary>
    Replace = 1,

    /// <summary>
    ///   Keep both, giving the imported one a free name such as
    ///   <c>DVD Order (2)</c>.
    /// </summary>
    KeepBoth = 2,
}
