using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Shoko.Core.API.Models
{
    public class AuthenticationModel
    {
        [Required, JsonProperty("grant_type")]
        public string GrantType { get; set; }
        [Required] public string Username { get; set; }
        public string Password { get; set; }
        /*[JsonProperty("client_id")]
        public string ClientId { get; set; }
        [JsonProperty("client_secret")]
        public string ClientSecret { get; set; }*/
    }
}
