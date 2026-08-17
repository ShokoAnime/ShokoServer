using System.Runtime.Serialization;

namespace Shoko.Server.Plex.Models.Login;

public class Location
{
    [DataMember(Name = "code")] public string Code { get; set; } = null!;
    [DataMember(Name = "country")] public string Country { get; set; } = null!;
    [DataMember(Name = "city")] public string City { get; set; } = null!;
    [DataMember(Name = "subdivisions")] public string Subdivisions { get; set; } = null!;
    [DataMember(Name = "coordinates")] public string Coordinates { get; set; } = null!;
}
