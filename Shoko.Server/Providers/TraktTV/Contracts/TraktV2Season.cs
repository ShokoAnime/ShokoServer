using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts
{
    [DataContract]
    public class TraktV2Season
    {
        [DataMember(Name = "number")]
        public int number { get; set; }

        [DataMember(Name = "ids")]
        public TraktV2SeasonIds ids { get; set; }

        [DataMember(Name = "images")]
        public TraktV2Images images { get; set; }

        [DataMember(Name = "episodes")]
        public List<TraktV2Episode> episodes { get; set; }
    }
}