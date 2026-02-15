using System.Runtime.Serialization;

namespace Shoko.Server.Plex.Models.Libraries;

public class Location
{
    [DataMember(Name = "id")] public long Id { get; set; }
    [DataMember(Name = "path")] public string Path { get; set; }
}
