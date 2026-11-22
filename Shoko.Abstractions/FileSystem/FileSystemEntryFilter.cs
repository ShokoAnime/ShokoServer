namespace Shoko.Abstractions.FileSystem;

/// <summary>
///   Decides whether a listing of the file system keeps an entry.
/// </summary>
/// <param name="entry">
///   The entry: its path and whether it is a directory are known at once, and
///   its stat is read only if the filter asks for it.
/// </param>
/// <returns>
///   <c>true</c> to keep it; <c>false</c> to leave it out, and for a directory
///   not to walk into it either.
/// </returns>
public delegate bool FileSystemEntryFilter(FileSystemEntry entry);
