using System.Runtime.Serialization;

namespace Shoko.Models.Plex.Login
{
    public class Roles
    {
        [DataMember(Name = "roles")] public string[] PurpleRoles { get; set; }
    }
}