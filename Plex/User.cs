using System;
using Newtonsoft.Json;

namespace Shoko.Commons.Plex.Login
{
    public class User
    {
        [JsonProperty("id")] public long Id { get; set; }
        [JsonProperty("uuid")] public string Uuid { get; set; }
        [JsonProperty("email")] public string Email { get; set; }
        [JsonProperty("joined_at")] public DateTime JoinedAt { get; set; }
        [JsonProperty("username")] public string Username { get; set; }
        [JsonProperty("title")] public string Title { get; set; }
        [JsonProperty("thumb")] public string Thumb { get; set; }
        [JsonProperty("hasPassword")] public bool HasPassword { get; set; }
        [JsonProperty("authToken")] public object AuthToken { get; set; }
        [JsonProperty("authentication_token")] public object AuthenticationToken { get; set; }
        [JsonProperty("subscription")] public Subscription Subscription { get; set; }
        [JsonProperty("roles")] public Roles Roles { get; set; }
        [JsonProperty("entitlements")] public string[] Entitlements { get; set; }
        [JsonProperty("confirmedAt")] public DateTime ConfirmedAt { get; set; }
        [JsonProperty("forumId")] public long ForumId { get; set; }
    }
}