using System;
using Newtonsoft.Json;

namespace Shoko.Commons.Plex.Login
{
    public class PlexKey
    {
        [JsonProperty("id")] public long Id { get; set; }
        [JsonProperty("code")] public string Code { get; set; }
        [JsonProperty("clientIdentifier")] public string ClientIdentifier { get; set; }
        [JsonProperty("location")] public Location Location { get; set; }
        [JsonProperty("expiresIn")] public long ExpiresIn { get; set; }
        [JsonProperty("createdAt")] public DateTime CreatedAt { get; set; }
        [JsonProperty("expiresAt")] public DateTime ExpiresAt { get; set; }
        [JsonProperty("authToken")] public string AuthToken { get; set; }
    }
}