using System;
using System.Runtime.Serialization;

namespace Shoko.Models.Plex.Login
{
    public class PlexKey
    {
        [DataMember(Name = "id")] public long Id { get; set; }
        [DataMember(Name = "code")] public string Code { get; set; }
        [DataMember(Name = "clientIdentifier")] public string ClientIdentifier { get; set; }
        [DataMember(Name = "location")] public Location Location { get; set; }
        [DataMember(Name = "expiresIn")] public long ExpiresIn { get; set; }
        [DataMember(Name = "createdAt")] public DateTime CreatedAt { get; set; }
        [DataMember(Name = "expiresAt")] public DateTime ExpiresAt { get; set; }
        [DataMember(Name = "authToken")] public string AuthToken { get; set; }
    }
}