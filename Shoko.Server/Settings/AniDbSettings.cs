using System;
using System.ComponentModel.DataAnnotations;
using Shoko.Models.Enums;

namespace Shoko.Server.Settings;

public class AniDbSettings
{
    [Required(AllowEmptyStrings = false)]
    public string Username { get; set; }

    [Required(AllowEmptyStrings = false)]
    public string Password { get; set; }

    [Required(AllowEmptyStrings = false)]
    public string HTTPServerUrl { get; set; } = "http://api.anidb.net:9001";

    public string UDPServerAddress { get; set; } = "api.anidb.net";

    public ushort UDPServerPort { get; set; } = 9000;

    // We set it to 60 seconds due to issues with UDP timeouts behind NAT.
    // 60 seconds is a good default for most users.
    public int UDPPingFrequency { get; set; } = 60;

    public ushort ClientPort { get; set; } = 4556;

    public string AVDumpKey { get; set; }

    public ushort AVDumpClientPort { get; set; } = 4557;

    public bool DownloadRelatedAnime { get; set; } = false;

    public bool DownloadReviews { get; set; } = false;

    public bool DownloadReleaseGroups { get; set; } = false;

    public bool MyList_AddFiles { get; set; } = true;

    public AniDBFile_State MyList_StorageState { get; set; } = AniDBFile_State.HDD;

    public AniDBFileDeleteType MyList_DeleteType { get; set; } = AniDBFileDeleteType.MarkUnknown;

    public bool MyList_ReadUnwatched { get; set; } = true;

    public bool MyList_ReadWatched { get; set; } = true;

    public bool MyList_SetWatched { get; set; } = true;

    public bool MyList_SetUnwatched { get; set; } = true;

    public int MyList_RetainedBackupCount { get; set; } = 30;

    public ScheduledUpdateFrequency MyList_UpdateFrequency { get; set; } = ScheduledUpdateFrequency.Never;

    public ScheduledUpdateFrequency Calendar_UpdateFrequency { get; set; } = ScheduledUpdateFrequency.Never;

    public ScheduledUpdateFrequency Anime_UpdateFrequency { get; set; } = ScheduledUpdateFrequency.Never;

    public ScheduledUpdateFrequency File_UpdateFrequency { get; set; } = ScheduledUpdateFrequency.Daily;

    public ScheduledUpdateFrequency Notification_UpdateFrequency { get; set; } = ScheduledUpdateFrequency.Never;

    public bool Notification_HandleMovedFiles { get; set; } = false;

    public bool DownloadCharacters { get; set; } = true;

    public bool DownloadCreators { get; set; } = true;

    [Range(0, 5, ErrorMessage = "Max Relation Depth may only be between 0 and 5")]
    public int MaxRelationDepth { get; set; } = 1;

    public int MinimumHoursToRedownloadAnimeInfo { get; set; } = 24;

    public bool AutomaticallyImportSeries { get; set; } = false;

    public AVDumpSettings AVDump { get; set; } = new();

    public AnidbRateLimitSettings HTTPRateLimit { get; set; } = new();

    public AnidbRateLimitSettings UDPRateLimit { get; set; } = new();
}
