using System.Runtime.Serialization;

namespace JMMServer.Providers.TraktTV.Contracts
{
    [DataContract(Name = "clearart")]
    public class TraktV2Clearart
    {
        public string full { get; set; }
    }
}