using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts
{
    [DataContract(Name = "clearart")]
    public class TraktV2Clearart
    {
        public string full { get; set; }
    }
}