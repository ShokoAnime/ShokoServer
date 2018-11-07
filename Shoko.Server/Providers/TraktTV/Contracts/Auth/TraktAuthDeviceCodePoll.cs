using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts.Auth
{
    [DataContract]
    public class TraktAuthDeviceCodePoll
    {
        [DataMember(Name = "client_id")]
        public string ClientID { get; set; }

        [DataMember(Name = "client_secret")]
        public string ClientSecret { get; set; }

        [DataMember(Name = "code")]
        public string DeviceCode { get; set; }

        public TraktAuthDeviceCodePoll()
        {
            ClientID = TraktConstants.ClientID;
            ClientSecret = TraktConstants.ClientSecret;
        }
    }
}
