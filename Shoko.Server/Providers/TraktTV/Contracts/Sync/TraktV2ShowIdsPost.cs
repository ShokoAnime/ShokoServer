using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts
{
    [DataContract]
    public class TraktV2ShowIdsPost
    {
        [DataMember(Name = "slug")]
        public string slug { get; set; }
    }
}