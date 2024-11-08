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
    TraktSync = 6,
    TraktUpdate = 7,
    AniDBFileUpdates = 10,
    TraktToken = 13,
    AniDBNotify = 15,
}

public enum TraktSyncAction
{
    Add = 1,
    Remove = 2
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

[Flags]
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
