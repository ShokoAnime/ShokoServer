using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts.Sync;

[DataContract]
public class TraktSyncWatchedSeason
{
    [DataMember(Name = "number")]
    public int Number { get; set; }

    [DataMember(Name = "episodes")]
    public List<TraktSyncWatchedEpisode> Episodes { get; set; }

    public override string ToString()
    {
        return Episodes != null
            ? $"Season: {Number} - Episodes Watched: {Episodes.Count}"
            : $"Season: {Number}";
    }
}
