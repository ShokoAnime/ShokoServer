using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts
{
    [DataContract(Name = "thumb")]
    public class TraktV2Thumb
    {
        public string full { get; set; }
    }
}