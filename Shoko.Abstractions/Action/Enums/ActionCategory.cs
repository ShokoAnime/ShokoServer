using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Action.Enums;

/// <summary>
///   Defines the category of an executable action. Categories group related
///   actions together in the UI and can be used to filter the action list.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum ActionCategory : byte
{
    /// <summary>
    ///   File import and scanning actions.
    /// </summary>
    Import = 0x01,

    /// <summary>
    ///   AniDB metadata and synchronization actions.
    /// </summary>
    AniDB = 0x21,

    /// <summary>
    ///   TMDB metadata and synchronization actions.
    /// </summary>
    TMDB = 0x22,

    /// <summary>
    ///   AniList metadata and synchronization actions.
    /// </summary>
    AniList = 0x23,

    /// <summary>
    ///   Data synchronization actions across providers.
    /// </summary>
    Sync = 0x31,

    /// <summary>
    ///   Image download and management actions.
    /// </summary>
    Images = 0x71,

    /// <summary>
    ///   System maintenance actions.
    /// </summary>
    Maintenance = 0xF1,

    /// <summary>
    ///   Uncategorized or miscellaneous actions. Actions which does not set
    ///   their own category will be placed in this category by default.
    /// </summary>
    Mischievous = 0xF2,

    /// <summary>
    ///   Destructive operations such as purging data.
    /// </summary>
    Destructive = 0xFE,

    /// <summary>
    ///   The category should be inferred from the plugin that provides the action.
    /// </summary>
    PluginInferred = 0xFF,
}
