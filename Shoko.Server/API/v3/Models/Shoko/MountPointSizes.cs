using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.API.v3.Models.Shoko;

/// <summary>
///   Sizes of a mount point, extending the child item counts with the space
///   on the underlying drive.
/// </summary>
public class MountPointSizes : ChildItems
{
    /// <summary>
    ///   Bytes available to the user the server runs as. May be less than the
    ///   free space on the drive when quotas or reserved blocks apply.
    /// </summary>
    [Required]
    public long AvailableBytes { get; set; }

    /// <summary>
    ///   Total size of the drive in bytes.
    /// </summary>
    [Required]
    public long TotalBytes { get; set; }
}
