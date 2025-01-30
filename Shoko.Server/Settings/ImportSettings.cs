using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.Settings;

public class ImportSettings
{
    public HasherSettings Hasher { get; set; } = new();

    public List<string> VideoExtensions { get; set; } =
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

    public List<string> Exclude { get; set; } =
    [
        @"[\\\/]\$RECYCLE\.BIN[\\\/]", @"[\\\/]\.Recycle\.Bin[\\\/]", @"[\\\/]\.Trash-\d+[\\\/]"
    ];

    public bool RunOnStart { get; set; } = false;

    /// <summary>
    /// Max auto-scan attempts per file for unrecognized files.
    /// </summary>
    [Range(0, 100)]
    public int MaxAutoScanAttemptsPerFile { get; set; } = 15;

    public bool ScanDropFoldersOnStart { get; set; } = false;

    public bool UseExistingFileWatchedStatus { get; set; } = true;

    public bool AutomaticallyDeleteDuplicatesOnImport { get; set; } = false;

    public bool FileLockChecking { get; set; } = true;

    public int FileLockWaitTimeMS { get; set; } = 4000;

    public bool AggressiveFileLockChecking { get; set; } = true;

    public int AggressiveFileLockWaitTimeSeconds { get; set; } = 8;

    public bool SkipDiskSpaceChecks { get; set; }

    public string MediaInfoPath { get; set; }

    public int MediaInfoTimeoutMinutes { get; set; } = 5;
}
