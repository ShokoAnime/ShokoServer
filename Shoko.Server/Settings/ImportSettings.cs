using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;

#nullable enable
namespace Shoko.Server.Settings;

public class ImportSettings
{
    private List<string> _internalVideoExtensions =
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
    [DefaultValue(new string[] { "MKV", "AVI", "MP4", "MOV", "OGM", "WMV", "MPG", "MPEG", "MK3D", "M4V" })]
    [List(UniqueItems = true, Sortable = true)]
    public List<string> VideoExtensions
    {
        get => _internalVideoExtensions;
        set => _internalVideoExtensions = value
            .Select(ext => "." + ext.ToLowerInvariant().TrimStart('.').Trim())
            .Distinct()
            .Except([string.Empty, null!])
            .ToList();
    }

    private List<string> _internalExclude =
    [
        @"[\\\/]\$RECYCLE\.BIN[\\\/]", @"[\\\/]\.Recycle\.Bin[\\\/]", @"[\\\/]\.Trash-\d+[\\\/]"
    ];

    /// <summary>
    /// List of regular expression patterns to exclude any files that match on the full path.
    /// </summary>
    [Display(Name = "Exclude Regex Patterns")]
    [DefaultValue(new string[] { @"[\\\/]\$RECYCLE\.BIN[\\\/]", @"[\\\/]\.Recycle\.Bin[\\\/]", @"[\\\/]\.Trash-\d+[\\\/]" })]
    [List(UniqueItems = true, Sortable = true)]
    public List<string> Exclude
    {
        get => _internalExclude;
        set
        {
            _internalExcludeRegexes = null;
            _internalExclude = value
                .Select(ext => string.IsNullOrWhiteSpace(ext) ? string.Empty : ext)
                .Distinct()
                .Except([string.Empty, null!])
                .ToList();
        }
    }

    private List<Regex>? _internalExcludeRegexes;

    /// <summary>
    /// List of regular expression instances to exclude any files that match on the full path.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<Regex> ExcludeExpressions
    {
        get
        {
            if (_internalExcludeRegexes is not null)
                return _internalExcludeRegexes;

            var excludes = new List<Regex>();
            foreach (var exclusion in _internalExclude)
            {
                try
                {
                    var regex = new Regex(exclusion, RegexOptions.Compiled);
                    excludes.Add(regex);
                }
                catch (Exception)
                {
                    // Logger.LogError(e, "Unable to compile exclusion regular expression: {Regex}", exclusion);
                }
            }

            return _internalExcludeRegexes = excludes;
        }
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
    /// Determines if we should clean up the managed folder structure when doing
    /// a scan of the folder by default if it's not overridden on a per job
    /// basis.
    /// </summary>
    [Display(Name = "Clean Up Structure on Managed Folder Scan")]
    public bool CleanUpStructure { get; set; } = false;

    /// <summary>
    /// Max auto-scan attempts per file for unrecognized files.
    /// </summary>
    [Badge("Advanced", Theme = DisplayColorTheme.Primary)]
    [Visibility(Size = DisplayElementSize.Small, Advanced = true)]
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
    [Badge("Debug", Theme = DisplayColorTheme.Warning)]
    [Visibility(Advanced = true)]
    [Display(Name = "Use File Lock Checking")]
    [RequiresRestart]
    [DefaultValue(true)]
    public bool FileLockChecking { get; set; } = true;

    /// <summary>
    /// Time between each check to see if a file is currently being written to.
    /// </summary>
    [Badge("Debug", Theme = DisplayColorTheme.Warning)]
    [Visibility(
        Advanced = true,
        DisableWhenMemberIsSet = nameof(FileLockChecking),
        DisableWhenSetTo = false
    )]
    [Display(Name = "File Lock Wait Time (ms)")]
    [RequiresRestart]
    [DefaultValue(4_000)]
    [Range(1_000, 60_000)]
    public int FileLockWaitTimeMS { get; set; } = 4_000;

    /// <summary>
    /// For file systems without proper locking, with this option enabled, Shoko
    /// will try to use a more aggressive method to check if a file is currently
    /// being written to.
    /// </summary>
    [Badge("Debug", Theme = DisplayColorTheme.Warning)]
    [Visibility(Advanced = true)]
    [Display(Name = "Use Aggressive File Lock Checking")]
    [RequiresRestart]
    [DefaultValue(true)]
    public bool AggressiveFileLockChecking { get; set; } = true;

    /// <summary>
    /// Time between each check to see if a file is currently being written to.
    /// </summary>
    [Visibility(
        Advanced = true,
        Size = DisplayElementSize.Small,
        DisableWhenMemberIsSet = nameof(AggressiveFileLockChecking),
        DisableWhenSetTo = false
    )]
    [Badge("Debug", Theme = DisplayColorTheme.Warning)]
    [Display(Name = "Aggressive File Lock Wait Time (seconds)")]
    [RequiresRestart]
    [DefaultValue(8)]
    [Range(0, 60)]
    public int AggressiveFileLockWaitTimeSeconds { get; set; } = 8;

    /// <summary>
    /// Skip disk space checks during the move/rename of files.
    /// </summary>
    [Badge("Debug", Theme = DisplayColorTheme.Warning)]
    [Visibility(Advanced = true)]
    [Display(Name = "Skip Disk Space Checks")]
    [DefaultValue(false)]
    public bool SkipDiskSpaceChecks { get; set; }

    /// <summary>
    /// Optional. Custom path to MediaInfo executable.
    /// </summary>
    [Badge("Advanced", Theme = DisplayColorTheme.Primary)]
    [Visibility(Size = DisplayElementSize.Full, Advanced = true)]
    [Display(Name = "Override MediaInfo Path")]
    [DefaultValue(null)]
    public string? MediaInfoPath { get; set; }

    /// <summary>
    /// Timeout for wait for MediaInfo to finish scanning a file before killing
    /// it.
    /// </summary>
    [Badge("Debug", Theme = DisplayColorTheme.Warning)]
    [Visibility(Size = DisplayElementSize.Small, Advanced = true)]
    [Display(Name = "MediaInfo Timeout (minutes)")]
    [DefaultValue(5)]
    [Range(1, 60)]
    public int MediaInfoTimeoutMinutes { get; set; } = 5;
}
