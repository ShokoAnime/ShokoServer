using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts;

[DataContract]
public class TraktV2Season
{
    [DataMember(Name = "number")]
    public int SeasonNumber { get; set; }

    [DataMember(Name = "ids")]
    public TraktV2SeasonIds IDs { get; set; }

    [DataMember(Name = "episodes")]
    public List<TraktV2Episode> Episodes { get; set; }
}
