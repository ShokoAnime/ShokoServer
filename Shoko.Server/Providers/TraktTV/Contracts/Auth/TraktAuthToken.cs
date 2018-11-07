using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts.Auth
{
    [DataContract]
    public class TraktAuthToken
    {
        [DataMember(Name = "access_token")]
        public string AccessToken { get; set; }

        [DataMember(Name = "token_type")]
        public string TokenType { get; set; }

        [DataMember(Name = "expires_in")]
        public string ExpiresIn { get; set; }

        [DataMember(Name = "refresh_token")]
        public string RefreshToken { get; set; }

        [DataMember(Name = "scope")]
        public string Scope { get; set; }

        [DataMember(Name = "created_at")]
        public string CreatedAt { get; set; }
    }
}