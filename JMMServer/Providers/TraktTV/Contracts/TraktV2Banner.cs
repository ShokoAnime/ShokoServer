using System.Runtime.Serialization;

namespace JMMServer.Providers.TraktTV.Contracts
{
    [DataContract(Name = "banner")]
    public class TraktV2Banner
    {
        public string full { get; set; }
    }
}