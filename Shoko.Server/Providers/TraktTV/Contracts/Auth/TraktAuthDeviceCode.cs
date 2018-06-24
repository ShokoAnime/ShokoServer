using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts
{
    [DataContract]
    public class TraktAuthDeviceCode
    {
        [DataMember(Name = "client_id")]
        public string ClientID { get; set; }

        public TraktAuthDeviceCode()
        {
            ClientID = TraktConstants.ClientID;
        }
    }
}