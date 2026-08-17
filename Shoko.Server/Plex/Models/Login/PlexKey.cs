using System;
using System.Runtime.Serialization;

namespace Shoko.Server.Plex.Models.Login;

public class PlexKey
{
    [DataMember(Name = "id")] public long Id { get; set; }
    [DataMember(Name = "code")] public string Code { get; set; } = null!;
    [DataMember(Name = "clientIdentifier")] public string ClientIdentifier { get; set; } = null!;
    [DataMember(Name = "location")] public Location Location { get; set; } = null!;
    [DataMember(Name = "expiresIn")] public long ExpiresIn { get; set; }
    [DataMember(Name = "createdAt")] public DateTime CreatedAt { get; set; }
    [DataMember(Name = "expiresAt")] public DateTime ExpiresAt { get; set; }
    [DataMember(Name = "authToken")] public string AuthToken { get; set; } = null!;
}
