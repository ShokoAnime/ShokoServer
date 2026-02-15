using System;
using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts.Sync;

[DataContract]
internal class TraktSyncHistoryItem
{
    [DataMember(Name = "ids")]
    public TraktIds IDs { get; set; }

    [DataMember(Name = "watched_at")]
    public DateTime? WatchedAt { get; set; }
}
