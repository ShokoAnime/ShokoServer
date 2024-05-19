using System;

namespace Shoko.Server.Server;

public enum HashSource
{
    DirectHash = 1, // the file was hashed by the user
    FileNameCache = 2 // the hash was retrieved from the web cache based on file name
}

public enum ScheduledUpdateType
{
    AniDBCalendar = 1,
    TvDBInfo = 2,
    AniDBUpdates = 3,
    AniDBTitles = 4,
    AniDBMyListSync = 5,
    TraktSync = 6,
    TraktUpdate = 7,
    MALUpdate = 8,
    AniDBMylistStats = 9,
    AniDBFileUpdates = 10,
    LogClean = 11,
    AzureUserInfo = 12,
    TraktToken = 13,
    DayFiltersUpdate = 14,
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
}
