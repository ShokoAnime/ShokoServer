using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMContracts.PlexAndKodi;

namespace JMMContracts.API.Models
{
    /// <summary>
    /// Model that represends Tag
    /// </summary>
    public class Tag
    {
        /// <summary>
        /// Parametraless constructor for XML Serialization
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string role { get; set; }

        /// <summary>
        /// Parametraless constructor for XML Serialization
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string value { get; set;
        }
        /// <summary>
        /// Parametraless constructor for XML Serialization
        /// </summary>
        public Tag()
        {

        }

        /// <summary>
        /// Contructor that create Metadata_Episode out of video
        /// </summary>
        public Tag(PlexAndKodi.Tag tag)
        {
            role = tag.Role;
            value = tag.Value;
        }

        public static explicit operator Tag(JMMContracts.PlexAndKodi.Tag tag_in)
        {
            Tag tag_out = new Tag();
            tag_out.role = tag_in.Role;
            tag_out.value = tag_in.Value;
            return tag_out;
        }
    }
}
