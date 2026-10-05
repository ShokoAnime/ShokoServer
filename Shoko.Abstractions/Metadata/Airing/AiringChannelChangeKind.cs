using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   What happened to a channel, as an <see cref="AiringChannelEventArgs"/>
///   tells it. <see cref="Registered"/>, <see cref="CountryTaken"/> and
///   <see cref="CountryMoved"/> come from providers registering channels and
///   writing schedules; the others come from calls to the service, such as an
///   admin editing the registry.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum AiringChannelChangeKind : byte
{
    /// <summary>
    ///   A provider named a channel nothing answered to, so it was registered.
    /// </summary>
    Registered = 0,

    /// <summary>
    ///   A TV station without a country turned out to be in one, as a provider
    ///   named it with that country. It either took the country and a new ID,
    ///   or was merged into the channel there answering to its name.
    /// </summary>
    CountryTaken = 1,

    /// <summary>
    ///   A provider moved a keyed schedule to the channel of the same name in
    ///   another country, and the channel it left, now empty, was merged into
    ///   that one, as one of the two had no country.
    /// </summary>
    CountryMoved = 2,

    /// <summary>
    ///   Other channels were merged into this one through
    ///   <see cref="Services.IAiringScheduleService.MergeChannels"/>.
    /// </summary>
    Merged = 3,

    /// <summary>
    ///   This channel's ID is gone through
    ///   <see cref="Services.IAiringScheduleService.MergeChannels"/>: it was
    ///   folded into another channel, or, as the target, took its sources'
    ///   country and with it a new ID.
    /// </summary>
    MergedAway = 4,

    /// <summary>
    ///   The aliases were replaced through
    ///   <see cref="Services.IAiringScheduleService.SetChannelAliases"/>.
    /// </summary>
    AliasesSet = 5,

    /// <summary>
    ///   Aliases were added through
    ///   <see cref="Services.IAiringScheduleService.AddChannelAliases"/>.
    /// </summary>
    AliasesAdded = 6,

    /// <summary>
    ///   Aliases were removed through
    ///   <see cref="Services.IAiringScheduleService.RemoveChannelAliases"/>.
    /// </summary>
    AliasesRemoved = 7,

    /// <summary>
    ///   The channel was hidden or shown through
    ///   <see cref="Services.IAiringScheduleService.SetChannelHidden"/>.
    /// </summary>
    HiddenChanged = 8,
}
