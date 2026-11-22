namespace Shoko.Abstractions.FileSystem;

/// <summary>
///   A mounted volume read further: what the system says about the volume an
///   entry is on, read when asked.
/// </summary>
public sealed record VolumeStat : VolumeMount
{
    /// <summary>
    ///   The path it was read for; the mount point when listed.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    ///   The volume, as in <see cref="StatIdentity.VolumeID"/>, so the two
    ///   compare directly.
    /// </summary>
    public required string VolumeID { get; init; }

    /// <summary>
    ///   The volume's label, when it has one and the platform tells; Windows
    ///   only.
    /// </summary>
    public string? Label { get; init; }

    /// <summary>
    ///   Its size, in bytes.
    /// </summary>
    public required long TotalSize { get; init; }

    /// <summary>
    ///   The free space, in bytes, space kept for the administrator included.
    /// </summary>
    public required long FreeSpace { get; init; }

    /// <summary>
    ///   The free space the server may use, in bytes.
    /// </summary>
    public required long AvailableFreeSpace { get; init; }
}
