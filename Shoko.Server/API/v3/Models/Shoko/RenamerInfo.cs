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
        /// <summary>
        /// The internal renamer ID, always unique
        /// </summary>
        public string Id { get; set; }
        /// <summary>
        /// If a priority is set, this will be the integert priority - lower is better
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)] public int? Priority { get; set; } = null;

        /// <summary>
        /// Human Readable description/name
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)] public string Description { get; set; }
        /// <summary>
        /// If the renamer is disabled.
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)] public bool Enabled { get; set; }
    }
}
