using System.Runtime.Serialization;

namespace Shoko.Server.Plex.Models.Login;

public class Subscription
{
    [DataMember(Name = "active")] public bool Active { get; set; }
    [DataMember(Name = "status")] public string Status { get; set; } = null!;
    [DataMember(Name = "plan")] public string Plan { get; set; } = null!;
    [DataMember(Name = "features")] public string[] Features { get; set; } = null!;
}
