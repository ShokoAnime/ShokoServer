using System.Runtime.Serialization;

namespace Shoko.Models.Plex.Login
{
    public class Subscription
    {
        [DataMember(Name = "active")] public bool Active { get; set; }
        [DataMember(Name = "status")] public string Status { get; set; }
        [DataMember(Name = "plan")] public string Plan { get; set; }
        [DataMember(Name = "features")] public string[] Features { get; set; }
    }
}