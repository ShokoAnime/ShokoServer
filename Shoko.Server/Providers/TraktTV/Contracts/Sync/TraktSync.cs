using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts.Sync;

[DataContract]
internal class TraktSync
{
    [DataMember(Name = "episodes")]
    public IReadOnlyList<TraktSyncHistoryItem> Episodes { get; set; }

    [DataMember(Name = "movies")]
    public IReadOnlyList<TraktSyncHistoryItem> Movies { get; set; }
}
