﻿using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts;

[DataContract]
public class TraktV2WatchedEpisode
{
    [DataMember(Name = "number")] public int number { get; set; }

    [DataMember(Name = "plays")] public int plays { get; set; }

    [DataMember(Name = "last_watched_at")] public string last_watched_at { get; set; }

    public override string ToString()
    {
        return string.Format("Ep#: {0} - Plays: {1} - Last Watched: {2}", number, plays, last_watched_at);
    }
}
