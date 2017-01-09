using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts
{
    [DataContract(Name = "banner")]
    public class TraktV2Banner
    {
        public string full { get; set; }
    }
}