using System;
using System.Runtime.Serialization;

namespace Shoko.Server.Plex.Models.Login;

public class User
{
    [DataMember(Name = "id")] public long Id { get; set; }
    [DataMember(Name = "uuid")] public string Uuid { get; set; } = null!;
    [DataMember(Name = "email")] public string Email { get; set; } = null!;
    [DataMember(Name = "joined_at")] public DateTime JoinedAt { get; set; }
    [DataMember(Name = "username")] public string Username { get; set; } = null!;
    [DataMember(Name = "title")] public string Title { get; set; } = null!;
    [DataMember(Name = "thumb")] public string Thumb { get; set; } = null!;
    [DataMember(Name = "hasPassword")] public bool HasPassword { get; set; }
    [DataMember(Name = "authToken")] public object AuthToken { get; set; } = null!;
    [DataMember(Name = "authentication_token")] public object AuthenticationToken { get; set; } = null!;
    [DataMember(Name = "subscription")] public Subscription Subscription { get; set; } = null!;
    [DataMember(Name = "roles")] public Roles Roles { get; set; } = null!;
    [DataMember(Name = "entitlements")] public string[] Entitlements { get; set; } = null!;
    [DataMember(Name = "confirmedAt")] public DateTime ConfirmedAt { get; set; }
    [DataMember(Name = "forumId")] public long ForumId { get; set; }
}
