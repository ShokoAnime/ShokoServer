using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Shoko.Server.API.v3.Models.Shoko
{
    public class Drive : Folder
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public DriveType DriveType { get; set; }
    }
}