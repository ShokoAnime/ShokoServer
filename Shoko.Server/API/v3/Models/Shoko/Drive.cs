using System.ComponentModel.DataAnnotations;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Shoko.Server.API.v3.Models.Shoko;

public class Drive : Folder
{
    [Required, JsonConverter(typeof(StringEnumConverter))]
    public DriveType Type { get; set; }

    /// <summary>
    ///   Child item counts and space of the mount point, or <c>null</c> when
    ///   it is not accessible.
    /// </summary>
    public new MountPointSizes? Sizes
    {
        get => base.Sizes as MountPointSizes;
        set => base.Sizes = value;
    }
}
