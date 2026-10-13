using System.Runtime.Serialization;

namespace Shoko.Plugin.LegacyPlex.Models.Login;

public class Roles
{
    [DataMember(Name = "roles")] public string[] PurpleRoles { get; set; } = null!;
}
