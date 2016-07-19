using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMContracts.API.Models
{
    /// <summary>
    /// Model that hold arts for object
    /// </summary>
    public class Art
    {
        /// <summary>
        /// Thumbnails url
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string thumb { get; set; }

        /// <summary>
        /// Fanarts url
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string fanart { get; set; }

        /// <summary>
        /// Posters url
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string banner { get; set; }

        /// <summary>
        /// Contructor that create Art out of Video
        /// </summary>
        public Art(PlexAndKodi.Video video)
        {
            if (video.Thumb != null)
            {
                thumb = video.Thumb;
            }
            if (video.Art != null)
            {
                fanart = video.Art;
            }
            //TODO APIv2: POSTER/Banner
            //if (video.Art != null)
            //{
            //    banner = video.Art;
            //}
        }

        /// <summary>
        /// Parametraless constructor for XML Serialization
        /// </summary>
        public Art()
        {

        }
    }
}
