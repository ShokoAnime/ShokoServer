using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Shoko.Core.API.Models
{
    public class OAuthResponse
    {
        [Required, JsonProperty("access_token")]
        public string AccessToken { get; set; }
        [Required, JsonProperty("token_type")]
        public string TokenType { get; set; } = "bearer";
        [JsonProperty("expires_in", Required = Required.DisallowNull)]
        public double ExpiresIn { get; set; }

        [JsonProperty("scope", Required = Required.DisallowNull)]
        public string Scope { get; set; }
    }
}
