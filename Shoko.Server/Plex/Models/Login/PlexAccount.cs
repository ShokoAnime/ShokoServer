using System.Runtime.Serialization;

namespace Shoko.Server.Plex.Models.Login;

public class PlexAccount
{
    [DataMember(Name = "user")] public User User { get; set; }
}
