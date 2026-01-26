using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts.Sync;

[DataContract]
internal class TraktSync
{
    [DataMember(Name = "episodes")]
    public List<TraktSyncHistoryItem> Episodes { get; set; } = [];

    [DataMember(Name = "movies")]
    public List<TraktSyncHistoryItem> Movies { get; set; } = [];
}
