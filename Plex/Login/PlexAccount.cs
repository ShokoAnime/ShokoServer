using Newtonsoft.Json;

namespace Shoko.Commons.Plex.Login
{
    public class PlexAccount
    {
        [JsonProperty("user")] public User User { get; set; }
    }
}