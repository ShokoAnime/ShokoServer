using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMContracts.API.Models
{

    /// <summary>
    /// Model that represents a Filter
    /// </summary>
    public class Filter
    {
        /// <summary>
        /// id - uniqe for Filter
        /// </summary>
        public string id { get; set; }

        /// <summary>
        /// title of Filter
        /// </summary>
        public string title { get; set; }

        /// <summary>
        /// key (url) for content of Filter
        /// </summary>
        public string key { get; set; }

        /// <summary>
        /// count of items that Filter contains
        /// </summary>
        public string count{ get; set; }

        /// <summary>
        /// holds art for current filter
        /// </summary>
        public JMMContracts.API.Models.Art art { get; set; }

        /// <summary>
        /// Contructor that create filter out of video
        /// </summary>
        public Filter(PlexAndKodi.Video video)
        {
            id = video.Id;
            title = video.Title;
            key = video.Key;
            count = video.LeafCount;
            art = new Art(video);
        }

        /// <summary>
        /// Parametraless constructor for XML Serialization
        /// </summary>
        public Filter()
        {

        }

    }
}
