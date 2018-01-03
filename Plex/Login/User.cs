using System;
using System.Runtime.Serialization;

namespace Shoko.Models.Plex.Login
{
    public class User
    {
        [DataMember(Name = "id")] public long Id { get; set; }
        [DataMember(Name = "uuid")] public string Uuid { get; set; }
        [DataMember(Name = "email")] public string Email { get; set; }
        [DataMember(Name = "joined_at")] public DateTime JoinedAt { get; set; }
        [DataMember(Name = "username")] public string Username { get; set; }
        [DataMember(Name = "title")] public string Title { get; set; }
        [DataMember(Name = "thumb")] public string Thumb { get; set; }
        [DataMember(Name = "hasPassword")] public bool HasPassword { get; set; }
        [DataMember(Name = "authToken")] public object AuthToken { get; set; }
        [DataMember(Name = "authentication_token")] public object AuthenticationToken { get; set; }
        [DataMember(Name = "subscription")] public Subscription Subscription { get; set; }
        [DataMember(Name = "roles")] public Roles Roles { get; set; }
        [DataMember(Name = "entitlements")] public string[] Entitlements { get; set; }
        [DataMember(Name = "confirmedAt")] public DateTime ConfirmedAt { get; set; }
        [DataMember(Name = "forumId")] public long ForumId { get; set; }
    }
}