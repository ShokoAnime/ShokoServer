using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts
{
    [DataContract(Name = "show")]
    public class TraktV2ShowPost
    {
        [DataMember(Name = "ids")]
        public TraktV2ShowIdsPost ids { get; set; }
    }
}