using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts.Scrobble;

[DataContract]
internal class TraktScrobble
{
    [DataMember(Name = "episode", EmitDefaultValue = false)]
    public TraktScrobbleItem Episode { get; set; }

    [DataMember(Name = "movie", EmitDefaultValue = false)]
    public TraktScrobbleItem Movie { get; set; }

    [DataMember(Name = "progress")]
    public float Progress { get; set; }
}
