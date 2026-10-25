using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Connectivity.Enums;

/// <summary>
///   Whether a release or relocation provider can be used now, and if not,
///   what stops it. The first that applies wins, in the order listed.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum ProviderAvailabilityKind : byte
{
    /// <summary>
    ///   The provider can be used.
    /// </summary>
    Available = 0,

    /// <summary>
    ///   An admin turned the provider off.
    /// </summary>
    Disabled = 1,

    /// <summary>
    ///   The provider says it lacks what it needs, such as credentials or an
    ///   API key.
    /// </summary>
    NotConfigured = 2,

    /// <summary>
    ///   A suspended suspension provider holds the provider back, such as
    ///   during a ban, until the suspension ends or is lifted.
    /// </summary>
    Suspended = 3,
}
