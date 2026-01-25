using System;
using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts.Sync;

[DataContract]
public class TraktSyncWatchedShowsResult
{
    [DataMember(Name = "plays")]
    public int Plays { get; set; }

    [DataMember(Name = "last_watched_at")]
    public DateTime LastWatchedAt { get; set; }

    [DataMember(Name = "show")]
    public TraktMediaItem Show { get; set; }

    [DataMember(Name = "seasons")]
    public TraktSyncWatchedSeason[] Seasons { get; set; }
}
