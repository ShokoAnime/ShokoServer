using System.Runtime.Serialization;

namespace JMMServer.Providers.TraktTV
{
    [DataContract]
    public class TraktV2RefreshToken
    {
        public TraktV2RefreshToken()
        {
            grant_type = "refresh_token";
            redirect_uri = "urn:ietf:wg:oauth:2.0:oob";
            client_id = TraktConstants.ClientID;
            client_secret = TraktConstants.ClientSecret;
        }

        [DataMember(Name = "refresh_token")]
        public string refresh_token { get; set; }

        [DataMember(Name = "client_id")]
        public string client_id { get; set; }

        [DataMember(Name = "client_secret")]
        public string client_secret { get; set; }

        [DataMember(Name = "redirect_uri")]
        public string redirect_uri { get; set; }

        [DataMember(Name = "grant_type")]
        public string grant_type { get; set; }
    }
}