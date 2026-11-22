namespace Shoko.Abstractions.FileSystem;

/// <summary>
///   What the file system calls a file: the volume it is on and its ID on
///   that volume. Two paths name the same file, a hard link of it included,
///   when both values match.
/// </summary>
/// <remarks>
///   Both values are opaque: lowercase hexadecimal without leading zeros, so
///   comparing them as strings is enough, and only equality means anything.
///   They are only comparable at the moment they are read: a Unix device
///   number can change across a reboot or a remount, and a file's ID can be
///   given to another file once it is deleted.
/// </remarks>
/// <param name="VolumeID">
///   The volume: the device number on Unix, the volume serial number on
///   Windows.
/// </param>
/// <param name="UniqueID">
///   The file on that volume: the inode number on Unix, the file ID on Windows
///   (128 bits on ReFS).
/// </param>
public sealed record StatIdentity(string VolumeID, string UniqueID);
