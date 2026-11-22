namespace Shoko.Abstractions.FileSystem;

/// <summary>
///   What the file system says about a directory. It has nothing a file does
///   not; the type tells the two apart.
/// </summary>
public sealed record DirectoryStat : FileSystemStat;
