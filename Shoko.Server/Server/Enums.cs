using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Shoko.Server.Server;

public enum HashSource
{
    DirectHash = 1, // the file was hashed by the user
    FileNameCache = 2 // the hash was retrieved from the web cache based on file name
}

public enum ScheduledUpdateType
{
    AniDBCalendar = 1,
    AniDBUpdates = 3,
    AniDBMyListSync = 5,
    TraktSendWatchStates = 6,
    TraktGetWatchStates = 7,
    AniDBFileUpdates = 10,
    AniDBNotify = 15,
}

public enum AniDBNotifyType
{
    Message = 1,
    Notification = 2,
}

public enum AniDBMessageType
{
    Normal = 0,
    Anonymous = 1,
    System = 2,
    Moderator = 3,
}

/// <summary>
/// Read status of messages and notifications
/// </summary>
[Flags]
public enum AniDBMessageFlags
{
    /// <summary>
    /// No flags
    /// </summary>
    None = 0,

    /// <summary>
    /// Marked as read on AniDB
    /// </summary>
    ReadOnAniDB = 1,

    /// <summary>
    /// Marked as read locally
    /// </summary>
    ReadOnShoko = 2,

    /// <summary>
    /// Is a file moved notification
    /// </summary>
    FileMoved = 4,

    /// <summary>
    /// Has the file move been handled
    /// </summary>
    FileMoveHandled = 8
}

// TODO: Simplify these values to not be power of 2 and instead be increments of 1. We don't need flag support anymore.
[JsonConverter(typeof(StringEnumConverter))]
public enum ForeignEntityType
{
    None = 0,
    Collection = 1,
    Movie = 2,
    Show = 4,
    Season = 8,
    Episode = 16,
    Company = 32,
    Studio = 64,
    Network = 128,
    Person = 256,
    Character = 512,
}

[JsonConverter(typeof(StringEnumConverter))]
public enum CreatorRoleType
{
    /// <summary>
    /// Voice actor or voice actress.
    /// </summary>
    Actor,

    /// <summary>
    /// This can be anything involved in writing the show.
    /// </summary>
    Staff,

    /// <summary>
    /// The studio responsible for publishing the show.
    /// </summary>
    Studio,

    /// <summary>
    /// The main producer(s) for the show.
    /// </summary>
    Producer,

    /// <summary>
    /// Direction.
    /// </summary>
    Director,

    /// <summary>
    /// Series Composition.
    /// </summary>
    SeriesComposer,

    /// <summary>
    /// Character Design.
    /// </summary>
    CharacterDesign,

    /// <summary>
    /// Music composer.
    /// </summary>
    Music,

    /// <summary>
    /// Responsible for the creation of the source work this show is detrived from.
    /// </summary>
    SourceWork,
}

[JsonConverter(typeof(StringEnumConverter))]
public enum CharacterAppearanceType
{
    Unknown = 0,
    Main_Character,
    Minor_Character,
    Background_Character,
    Cameo
}

[JsonConverter(typeof(StringEnumConverter))]
public enum CharacterType
{
    Unknown = 0,
    Character = 1,
    // ??? = 2,
    Organization = 3,
}

[JsonConverter(typeof(StringEnumConverter))]
public enum ReleaseChannel
{
    Auto = 0,
    Stable = 1,
    Dev = 2,
    Debug = 3,
}
