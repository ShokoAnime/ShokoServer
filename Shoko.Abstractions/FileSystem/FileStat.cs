namespace Shoko.Abstractions.FileSystem;

/// <summary>
///   What the file system says about a file.
/// </summary>
public sealed record FileStat : FileSystemStat
{
    /// <summary>
    ///   The size of its contents, in bytes.
    /// </summary>
    public required long Size { get; init; }

    /// <summary>
    ///   The space it takes on disk, in bytes: less than <see cref="Size"/>
    ///   for a sparse or compressed file, more when rounded up to whole
    ///   blocks. <c>null</c> when the platform cannot tell.
    /// </summary>
    public long? AllocatedSize { get; init; }
}
