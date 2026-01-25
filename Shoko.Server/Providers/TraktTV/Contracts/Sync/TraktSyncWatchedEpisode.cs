using System;
using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts.Sync;

[DataContract]
public class TraktSyncWatchedEpisode
{
    [DataMember(Name = "number")]
    public int Number { get; set; }

    [DataMember(Name = "plays")]
    public int Plays { get; set; }

    [DataMember(Name = "last_watched_at")]
    public DateTime LastWatchedAt { get; set; }

    public override string ToString()
    {
        return $"Episode #: {Number} - Plays: {Plays} - Last Watched: {LastWatchedAt}";
    }
}
