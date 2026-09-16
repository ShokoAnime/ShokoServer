using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   What a provider did during a refresh.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum AiringScheduleRefreshState : byte
{
    /// <summary>
    ///   The provider did work for the entity.
    /// </summary>
    Refreshed = 0,

    /// <summary>
    ///   The provider had nothing to do for the entity, such as a series it
    ///   cannot key on.
    /// </summary>
    Skipped = 1,

    /// <summary>
    ///   The provider did not finish in time.
    /// </summary>
    TimedOut = 2,

    /// <summary>
    ///   The refresh was cancelled before the provider finished.
    /// </summary>
    Cancelled = 3,

    /// <summary>
    ///   The provider threw. The other providers are unaffected.
    /// </summary>
    Failed = 4,
}
