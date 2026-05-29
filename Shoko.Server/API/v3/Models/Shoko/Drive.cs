#nullable enable
using System.ComponentModel.DataAnnotations;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Shoko.Server.API.v3.Models.Shoko;

public class Drive : Folder
{
    [Required, JsonConverter(typeof(StringEnumConverter))]
    public DriveType Type { get; set; }
}
