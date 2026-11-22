namespace Shoko.Abstractions.FileSystem;

/// <summary>
///   A mounted volume, as the system's list of mounts gives it, without
///   reading the volume itself.
/// </summary>
/// <remarks>
///   Listing never touches the volumes, so a share whose server is gone
///   cannot hold it up. A <see cref="VolumeStat"/> is a mount read further:
///   its ID, label, size and free space.
/// </remarks>
public record VolumeMount
{
    /// <summary>
    ///   Where the volume is mounted: its root folder.
    /// </summary>
    public required string MountPoint { get; init; }

    /// <summary>
    ///   What is mounted there: the device or the share, when the platform
    ///   tells. Linux, and Windows for a drive letter mapped to a share.
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    ///   The file system's name, when known.
    /// </summary>
    public string? FormatName { get; init; }

    /// <summary>
    ///   What kind of storage it is.
    /// </summary>
    public required VolumeKind Kind { get; init; }

    /// <summary>
    ///   Whether it is mounted read-only, when known: from the list on Linux,
    ///   and always on a <see cref="VolumeStat"/>.
    /// </summary>
    public bool? IsReadOnly { get; init; }

    /// <summary>
    ///   Whether it is part of the system rather than storage a library could
    ///   live on: a pseudo file system of the kernel or the desktop, a mount
    ///   under <c>/proc</c>, <c>/sys</c>, <c>/dev</c>, <c>/boot</c> or
    ///   <c>/efi</c>, or on Windows a drive of no known kind. Left out of <see cref="Services.IFileSystem.GetVolumeMounts"/>
    ///   unless asked for.
    /// </summary>
    public required bool IsSystem { get; init; }
}
