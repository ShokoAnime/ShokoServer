using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.Config.Attributes;
using Shoko.Plugin.Abstractions.Config.Enums;

namespace Shoko.Server.Settings;

[Section(DisplaySectionType.Minimal)]
[CustomAction("Test", Theme = DisplayColorTheme.Primary, Position = DisplayButtonPosition.Top, SectionName = "Login")]
public class AniDbSettings
{
    /// <summary>
    /// AniDB username.
    /// </summary>
    [SectionName("Login")]
    [Required(AllowEmptyStrings = false)]
    public string Username { get; set; }

    /// <summary>
    /// AniDB password.
    /// </summary>
    [SectionName("Login")]
    [PasswordPropertyText]
    [Required(AllowEmptyStrings = false)]
    public string Password { get; set; }

    /// <summary>
    /// Download character images from AniDB.
    /// </summary>
    [SectionName("Download")]
    [Display(Name = "Download Character Images")]
    public bool DownloadCharacters { get; set; } = true;

    /// <summary>
    /// Download creator images and extra data from AniDB. The extra data is
    /// needed for staff to get images and to properly detect studios among
    /// other staff, etc..
    /// </summary>
    [SectionName("Download")]
    [Display(Name = "Download Creator Images & Data")]
    public bool DownloadCreators { get; set; } = true;

    /// <summary>
    /// Always download related anime, regardless of if the auto-group setting
    /// is enabled.
    /// </summary>
    [SectionName("Download")]
    [Display(Name = "Always Download Related Anime")]
    public bool DownloadRelatedAnime { get; set; } = false;

    /// <summary>
    /// Max relation depth when scheduling anime to be updated/fetched.
    /// </summary>
    [SectionName("Download")]
    [Visibility(Size = DisplayElementSize.Small)]
    [Display(Name = "Max Relation Depth")]
    [Range(0, 5, ErrorMessage = "Max Relation Depth may only be between 0 and 5")]
    public int MaxRelationDepth { get; set; } = 1;

    /// <summary>
    /// The minimum number of hours to wait before attempting to re-downloading
    /// an AniDB anime.
    /// </summary>
    [SectionName("Download")]
    [Visibility(Size = DisplayElementSize.Small)]
    [Display(Name = "Minimum Hours To Redownload Anime Info")]
    [DefaultValue(24)]
    [Range(0, 48, ErrorMessage = "Minimum Hours To Redownload Anime Info may only be between 0 and 48")]
    public int MinimumHoursToRedownloadAnimeInfo { get; set; } = 24;

    /// <summary>
    /// Automatically create a Shoko Series for each AniDB anime.
    /// </summary>
    [Badge("Advanced", Theme = DisplayColorTheme.Important)]
    [SectionName("Download")]
    [Display(Name = "Automatically Import Series")]
    public bool AutomaticallyImportSeries { get; set; } = false;

    [Display(Name = "Add Files")]
    [SectionName("MyList")]
    public bool MyList_AddFiles { get; set; } = true;

    [Display(Name = "Read Watched")]
    [SectionName("MyList")]
    public bool MyList_ReadWatched { get; set; } = true;

    [Display(Name = "Read Unwatched")]
    [SectionName("MyList")]
    public bool MyList_ReadUnwatched { get; set; } = true;

    [Display(Name = "Set Watched")]
    [SectionName("MyList")]
    public bool MyList_SetWatched { get; set; } = true;

    [Display(Name = "Set Unwatched")]
    [SectionName("MyList")]
    public bool MyList_SetUnwatched { get; set; } = true;

    [Display(Name = "Storage State")]
    [SectionName("MyList")]
    public AniDBFile_State MyList_StorageState { get; set; } = AniDBFile_State.HDD;

    [Display(Name = "Delete Type")]
    [SectionName("MyList")]
    public AniDBFileDeleteType MyList_DeleteType { get; set; } = AniDBFileDeleteType.MarkUnknown;

    /// <summary>
    /// Number of days to retain backups of the downloaded MyList for the user.
    /// </summary>
    [SectionName("MyList")]
    [Visibility(Size = DisplayElementSize.Small)]
    [Display(Name = "Retained Backup Count")]
    [Range(0, 99, ErrorMessage = "MyList_RetainedBackupCount may only be between 0 and 99")]
    public int MyList_RetainedBackupCount { get; set; } = 30;

    /// <summary>
    /// Check which AniDB anime is currently airing in the next/previous week,
    /// and schedule an update for all of them, adding them to the local
    /// collection if they're not already part of it.
    /// </summary>
    [SectionName("Update")]
    [Display(Name = "Calendar")]
    public ScheduledUpdateFrequency Calendar_UpdateFrequency { get; set; } = ScheduledUpdateFrequency.Never;

    /// <summary>
    /// Check which AniDB anime has been updated since the last time we asked,
    /// and schedule an update for any in the local collection.
    /// </summary>
    [SectionName("Update")]
    [Display(Name = "Anime Updates")]
    public ScheduledUpdateFrequency Anime_UpdateFrequency { get; set; } = ScheduledUpdateFrequency.Never;

    /// <summary>
    /// Check for any files with missing info and schedule them for a re-check.
    /// </summary>
    [SectionName("Update")]
    [Display(Name = "Files with missing info")]
    public ScheduledUpdateFrequency File_UpdateFrequency { get; set; } = ScheduledUpdateFrequency.Daily;

    /// <summary>
    /// Sync the MyList with the local collection.
    /// </summary>
    [SectionName("Update")]
    [Display(Name = "MyList")]
    [Visibility(
        Visibility = DisplayVisibility.Visible,
        ToggleWhenMemberIsSet = nameof(MyList_UpdateFrequency),
        ToggleWhenSetTo = ScheduledUpdateFrequency.Never,
        ToggleVisibilityTo = DisplayVisibility.Disabled
    )]
    public ScheduledUpdateFrequency MyList_UpdateFrequency { get; set; } = ScheduledUpdateFrequency.Never;

    /// <summary>
    /// Check for any unread notifications and messages and download them if
    /// there are any.
    /// </summary>
    [SectionName("Update")]
    [Display(Name = "Notifications & Messages")]
    public ScheduledUpdateFrequency Notification_UpdateFrequency { get; set; } = ScheduledUpdateFrequency.Never;

    /// <summary>
    /// Handle 'File has been moved' messages from AniDB.
    /// </summary>
    [SectionName("Update")]
    [Display(Name = "Handle Moved Files")]
    public bool Notification_HandleMovedFiles { get; set; } = false;

    /// <summary>
    /// HTTP API server to communicate with.
    /// </summary>
    [SectionName("HTTP")]
    [Display(Name = "Server URL")]
    [Url]
    [Required(AllowEmptyStrings = false)]
    [Visibility(Size = DisplayElementSize.Large)]
    public string HTTPServerUrl { get; set; } = "http://api.anidb.net:9001";

    /// <summary>
    /// Settings for rate limiting the HTTP API.
    /// </summary>
    [SectionName("HTTP")]
    [DisplayName("Rate Limiting")]
    public AnidbRateLimitSettings HTTPRateLimit { get; set; } = new();

    /// <summary>
    /// UDP server to communicate with.
    /// </summary>
    [SectionName("UDP")]
    [Display(Name = "Server Address")]
    public string UDPServerAddress { get; set; } = "api.anidb.net";

    /// <summary>
    /// UDP server port to communicate with.
    /// </summary>
    [Display(Name = "Server Port")]
    [SectionName("UDP")]
    public ushort UDPServerPort { get; set; } = 9000;

    [Display(Name = "Client Port")]
    [SectionName("UDP")]
    public ushort ClientPort { get; set; } = 4556;

    // We set it to 60 seconds due to issues with UDP timeouts behind NAT.
    // 60 seconds is a good default for most users.
    [SectionName("UDP")]
    [Display(Name = "Ping Frequency (seconds)")]
    [Range(30, 120, ErrorMessage = "UDP Ping Frequency may only be between 1 and 60")]
    public int UDPPingFrequency { get; set; } = 60;

    /// <summary>
    /// Settings for rate limiting the UDP API.
    /// </summary>
    [SectionName("UDP")]
    [DisplayName("Rate Limiting")]
    public AnidbRateLimitSettings UDPRateLimit { get; set; } = new();

    /// <summary>
    /// The API key to use when using AVDump to dump files for AniDB. The key is
    /// created on their site and is separate from the username and password.
    /// </summary>
    [SectionName("AVDump")]
    [Display(Name = "API Key")]
    [PasswordPropertyText]
    public string AVDumpKey { get; set; }

    /// <summary>
    /// The client port to prefer binding to when using AVDump to dump files for
    /// AniDB.
    /// </summary>
    [SectionName("AVDump")]
    [Display(Name = "Client Port")]
    public ushort AVDumpClientPort { get; set; } = 4557;

    /// <summary>
    /// AVDump settings
    /// </summary>
    [SectionName("AVDump")]
    [DisplayName("Advanced AVDump Settings")]
    public AVDumpSettings AVDump { get; set; } = new();
}
