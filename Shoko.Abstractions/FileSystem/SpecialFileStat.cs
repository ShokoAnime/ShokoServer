namespace Shoko.Abstractions.FileSystem;

/// <summary>
///   What the file system says about a special file: a pipe, a socket or a
///   device. Only Unix keeps these in the file system.
/// </summary>
public sealed record SpecialFileStat : FileSystemStat
{
    /// <summary>
    ///   What kind of special file it is.
    /// </summary>
    public required SpecialFileKind Kind { get; init; }

    /// <summary>
    ///   For a device, the device it stands for, as an opaque lowercase
    ///   hexadecimal number like <see cref="StatIdentity.VolumeID"/>;
    ///   otherwise <c>null</c>.
    /// </summary>
    public string? DeviceID { get; init; }
}
