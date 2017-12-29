using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts
{
    [DataContract]
    public class TraktAuthPIN
    {
        [DataMember(Name = "code")]
        public string PINCode { get; set; }

        [DataMember(Name = "client_id")]
        public string ClientID { get; set; }

        [DataMember(Name = "client_secret")]
        public string ClientSecret { get; set; }

        [DataMember(Name = "grant_type")]
        public string GrantType { get; set; }

        [DataMember(Name = "redirect_uri")]
        public string RedirectURI { get; set; }

        public TraktAuthPIN()
        {
            GrantType = "authorization_code";
            RedirectURI = "urn:ietf:wg:oauth:2.0:oob";
            ClientID = TraktConstants.ClientID;
            ClientSecret = TraktConstants.ClientSecret;
        }
    }
}