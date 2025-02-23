using System.Runtime.Serialization;

namespace Shoko.Models.Plex.Login
{
    public class PlexAccount
    {
        [DataMember(Name = "user")] public User User { get; set; }
    }
}