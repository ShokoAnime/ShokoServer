using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Shoko.Plugin.Abstractions.Config.Attributes;
using Shoko.Plugin.Abstractions.Config.Enums;

namespace Shoko.Server.Settings;

public class ImportSettings
{
    private string[] InternalVideoExtensions { get; set; } =
    [
        "MKV",
        "AVI",
        "MP4",
        "MOV",
        "OGM",
        "WMV",
        "MPG",
        "MPEG",
        "MK3D",
        "M4V"
    ];

    /// <summary>
    /// List of video file extensions to import.
    /// </summary>
    [Visibility(Size = DisplayElementSize.Large)]
    [MinLength(1)]
    [List(ListType = DisplayListType.Comma, UniqueItems = true, Sortable = true)]
    public string[] VideoExtensions
    {
        get => InternalVideoExtensions;
        set => InternalVideoExtensions = value
            .Select(ext => ext.StartsWith('.') ? ext.TrimStart('.').ToUpper().Trim() : ext.ToUpper().Trim())
            .Distinct()
            .Except([string.Empty, null])
            .ToArray();
    }

    private string[] InternalExclude { get; set; } =
    [
        @"[\\\/]\$RECYCLE\.BIN[\\\/]", @"[\\\/]\.Recycle\.Bin[\\\/]", @"[\\\/]\.Trash-\d+[\\\/]"
    ];

    /// <summary>
    /// List of regular expression patterns to exclude any files that match on the full path.
    /// </summary>
    [Display(Name = "Exclude Regex Patterns")]
    [DefaultValue(new string[] { @"[\\\/]\$RECYCLE\.BIN[\\\/]", @"[\\\/]\.Recycle\.Bin[\\\/]", @"[\\\/]\.Trash-\d+[\\\/]" })]
    [List(UniqueItems = true, Sortable = true)]
    public string[] Exclude
    {
        get => InternalExclude;
        set => InternalExclude = value
            .Select(ext => string.IsNullOrWhiteSpace(ext) ? string.Empty : ext)
            .Distinct()
            .Except([string.Empty, null])
            .ToArray();
    }

    /// <summary>
    /// Run the import scheduled task on startup.
    /// </summary>
    [Display(Name = "Run Import on Startup")]
    public bool RunOnStart { get; set; } = false;

    /// <summary>
    /// Scan all source managed folders on server startup.
    /// </summary>
    [Display(Name = "Scan Source Managed Folders on Startup")]
    public bool ScanDropFoldersOnStart { get; set; } = false;

    /// <summary>
    /// Max auto-scan attempts per file for unrecognized files.
    /// </summary>
    [Visibility(Size = DisplayElementSize.Small)]
    [Display(Name = "Max auto-scan attempts per file")]
    [DefaultValue(15)]
    [Range(0, 100)]
    public int MaxAutoScanAttemptsPerFile { get; set; } = 15;

    /// <summary>
    /// Use the existing episode watched status when importing files.
    /// </summary>
    [Display(Name = "Use Existing Watched Status")]
    public bool UseExistingFileWatchedStatus { get; set; } = true;

    /// <summary>
    /// Automatically delete duplicate files on import after hashing them.
    /// </summary>
    [Display(Name = "Automatically Delete Duplicates on Import")]
    public bool AutomaticallyDeleteDuplicatesOnImport { get; set; } = false;

    /// <summary>
    /// Check if a file is currently being written to when reacting to events in the file watcher.
    /// </summary>
    [Display(Name = "Use File Lock Checking")]
    public bool FileLockChecking { get; set; } = true;

    /// <summary>
    /// Time between each check to see if a file is currently being written to.
    /// </summary>
    [Visibility(
        DisplayVisibility.Disabled,
        ToggleWhenMemberIsSet = nameof(FileLockChecking),
        ToggleWhenSetTo = true,
        ToggleVisibilityTo = DisplayVisibility.Visible
    )]
    [Display(Name = "File Lock Wait Time (ms)")]
    [DefaultValue(4_000)]
    [Range(1_000, 60_000)]
    public int FileLockWaitTimeMS { get; set; } = 4_000;

    /// <summary>
    /// For file systems without proper locking, with this option enabled, Shoko
    /// will try to use a more aggressive method to check if a file is currently
    /// being written to.
    /// </summary>
    [Display(Name = "Use Aggressive File Lock Checking")]
    public bool AggressiveFileLockChecking { get; set; } = true;

    /// <summary>
    /// Time between each check to see if a file is currently being written to.
    /// </summary>
    [Visibility(
        DisplayVisibility.Disabled,
        Size = DisplayElementSize.Small,
        ToggleWhenMemberIsSet = nameof(AggressiveFileLockChecking),
        ToggleWhenSetTo = true,
        ToggleVisibilityTo = DisplayVisibility.Visible
    )]
    [Display(Name = "Aggressive File Lock Wait Time (seconds)")]
    [DefaultValue(8)]
    [Range(0, 60)]
    public int AggressiveFileLockWaitTimeSeconds { get; set; } = 8;

    /// <summary>
    /// Skip disk space checks during the move/rename of files.
    /// </summary>
    [Display(Name = "Skip Disk Space Checks")]
    public bool SkipDiskSpaceChecks { get; set; }

    /// <summary>
    /// Optional. Custom path to MediaInfo executable.
    /// </summary>
    [Visibility(Size = DisplayElementSize.Full)]
    [Display(Name = "Override MediaInfo Path")]
    public string MediaInfoPath { get; set; }

    /// <summary>
    /// Timeout for wait for MediaInfo to finish scanning a file before killing
    /// it.
    /// </summary>
    [Visibility(Size = DisplayElementSize.Small)]
    [Display(Name = "MediaInfo Timeout (minutes)")]
    [DefaultValue(5)]
    [Range(1, 60)]
    public int MediaInfoTimeoutMinutes { get; set; } = 5;
}
