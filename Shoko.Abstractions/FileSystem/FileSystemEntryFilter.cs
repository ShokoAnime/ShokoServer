namespace Shoko.Abstractions.FileSystem;

/// <summary>
///   Decides whether a listing of the file system keeps an entry.
/// </summary>
/// <param name="path">The entry's absolute path.</param>
/// <param name="isDirectory">Whether the entry is a directory.</param>
/// <returns>
///   <c>true</c> to keep it; <c>false</c> to leave it out, and for a directory
///   not to walk into it either.
/// </returns>
public delegate bool FileSystemEntryFilter(string path, bool isDirectory);
