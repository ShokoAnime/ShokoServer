using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts
{
    [DataContract(Name = "logo")]
    public class TraktV2Logo
    {
        public string full { get; set; }
    }
}