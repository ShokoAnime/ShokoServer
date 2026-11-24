namespace Shoko.Abstractions.Video;

/// <summary>
///   How a managed folder is scanned
///   (<see cref="Services.IVideoService.ScanManagedFolder"/> and the methods
///   scheduling scans).
/// </summary>
public sealed record ManagedFolderScanOptions
{
    /// <summary>
    ///   The options with every default.
    /// </summary>
    public static ManagedFolderScanOptions Default { get; } = new();

    /// <summary>
    ///   The relative path within the managed folder to scan; the whole
    ///   folder when <c>null</c>. Not used when scanning every managed folder.
    /// </summary>
    public string? RelativePath { get; init; }

    /// <summary>
    ///   Whether to only scan for new files. Such a scan only adds files: it
    ///   keeps the records of missing files, which a full scan removes. When
    ///   <c>null</c>, a single folder is scanned whole, and a scan of every
    ///   managed folder scans the drop sources whole and the rest for new
    ///   files only.
    /// </summary>
    public bool? OnlyNewFiles { get; init; }

    /// <summary>
    ///   Whether to skip the events and the provider-specific post-save state
    ///   sync for the files found.
    /// </summary>
    public bool SkipEvents { get; init; }

    /// <summary>
    ///   Whether to clean up the managed folder afterwards, removing any empty
    ///   folders from the bottom up; the global setting when <c>null</c>.
    /// </summary>
    public bool? CleanUpStructure { get; init; }

    /// <summary>
    ///   Whether to look again at known files whose size changed; the global
    ///   setting when <c>null</c>. <c>true</c> reads the size of EVERY file in
    ///   the existing collection. Not used when only scanning for new files.
    /// </summary>
    public bool? CheckFileSize { get; init; }

    /// <summary>
    ///   Whether to look at each video again even when it has exceeded the
    ///   maximum allowed auto-scan attempts.
    /// </summary>
    public bool ForceScan { get; init; }
}
