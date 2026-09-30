using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Actions;

/// <summary>
///   The category of an executable action. Categories group related actions
///   together in the UI and can be used to filter the action list.
/// </summary>
/// <remarks>
///   This is a closed, core-owned enum — a plugin cannot invent a new
///   core-owned category at runtime; adding one requires a PR against core.
///   <see cref="Miscellaneous"/> is the shared fallback for any action that
///   declares no category. <see cref="PluginInferred"/> is an explicit opt-in
///   a plugin uses to request its own dedicated group; its display label is
///   always the owning plugin's own name.
/// </remarks>
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
    ///   The shared fallback category for any action — core or plugin — that
    ///   declares no category.
    /// </summary>
    Miscellaneous = 0xF2,

    /// <summary>
    ///   Destructive operations such as purging data.
    /// </summary>
    Destructive = 0xFE,

    /// <summary>
    ///   An explicit opt-in a plugin uses to request its own dedicated group.
    ///   The display label is always the owning plugin's own name.
    /// </summary>
    PluginInferred = 0xFF,
}
