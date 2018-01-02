using Newtonsoft.Json;

namespace Shoko.Commons.Plex.Login
{
    public class Roles
    {
        [JsonProperty("roles")] public string[] PurpleRoles { get; set; }
    }
}