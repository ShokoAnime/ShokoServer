using System;
using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts.Sync;

[DataContract]
internal class TraktSyncWatchedMoviesResult
{
    [DataMember(Name = "plays")]
    public int Plays { get; set; }

    [DataMember(Name = "last_watched_at")]
    public DateTime LastWatchedAt { get; set; }

    [DataMember(Name = "movie")]
    public TraktMediaItem Movie { get; set; }

    public override string ToString()
    {
        return $"TraktSyncWatchedMoviesResult - {Movie.Title} - Last Watched: {LastWatchedAt}";
    }
}
