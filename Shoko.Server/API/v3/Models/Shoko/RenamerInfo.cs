using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text;

namespace Shoko.Server.API.v3.Models.Shoko
{
    public class RenamerInfo
    {
        public string Id { get; set; }
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)] public int Priority { get; set; }
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)] public string Description { get; set; }
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)] public bool Enabled { get; set; }
    }
}
