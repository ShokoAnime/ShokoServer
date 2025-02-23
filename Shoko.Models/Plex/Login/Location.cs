using System.Runtime.Serialization;

namespace Shoko.Models.Plex.Login
{
    public class Location
    {
        [DataMember(Name = "code")] public string Code { get; set; }
        [DataMember(Name = "country")] public string Country { get; set; }
        [DataMember(Name = "city")] public string City { get; set; }
        [DataMember(Name = "subdivisions")] public string Subdivisions { get; set; }
        [DataMember(Name = "coordinates")] public string Coordinates { get; set; }
    }
}