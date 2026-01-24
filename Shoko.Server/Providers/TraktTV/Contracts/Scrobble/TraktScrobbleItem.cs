using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts.Scrobble;

[DataContract]
internal class TraktScrobbleItem
{
    [DataMember(Name = "ids")] public TraktIds Ids { get; set; }
}
