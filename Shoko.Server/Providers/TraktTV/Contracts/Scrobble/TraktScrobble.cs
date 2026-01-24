using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts.Scrobble;

[DataContract]
internal class TraktScrobble
{
    [DataMember(Name = "episode")]
    public TraktScrobbleItem Episode { get; set; }

    [DataMember(Name = "movie")]
    public TraktScrobbleItem Movie { get; set; }

    [DataMember(Name = "progress")]
    public float Progress { get; set; }
}
