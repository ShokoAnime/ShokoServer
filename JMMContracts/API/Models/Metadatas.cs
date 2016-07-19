using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMContracts.API.Models
{
    /// <summary>
    /// Model that represends MetaDatas
    /// </summary>
    public class Metadatas
    {
        /// <summary>
        /// Metadata count
        /// </summary>
        public int count { get; set; }

        /// <summary>
        /// Title of executed query/qroup
        /// </summary>
        public string title { get; set; }

        /// <summary>
        /// Hold video list
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<JMMContracts.API.Models.Video> videos { get; set; }

        /// <summary>
        /// Hold Filter list
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<JMMContracts.API.Models.Metadata> metadatas { get; set; }

        /// <summary>
        /// Hold series list
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<JMMContracts.API.Models.Serie> series { get; set; }

        /// <summary>
        /// Parametraless constructor for XML Serialization
        /// </summary>
        public Metadatas()
        {

        }

        /// <summary>
        /// Constructor that create Metadatas of given type out of MediaContainer
        /// </summary>
        public Metadatas(string type, JMMContracts.PlexAndKodi.MediaContainer mc)
        {
            title = mc.Title1;
            //TODO APIv2: Why MediaContainer Size is string ?! 
            int res = 0;
            int.TryParse(mc.Size, out res);
            count = res;
            if (mc.Childrens != null)
            {
                metadatas = new List<Metadata>();
                series = new List<Serie>();
                videos = new List<Video>();
                foreach (PlexAndKodi.Video video in mc.Childrens)
                {
                    switch (type)
                    {
                        //inside serie
                        case "3":
                            videos.Add(new Video(video));
                            break;
                        //series
                        case "0":
                        //series that are included in filter
                        case "5":
                            series.Add(new Serie(video));
                            break;
                        default:
                            metadatas.Add(new Metadata(video));
                            break;
                    }
                }
                if (metadatas.Count == 0)
                {
                    metadatas = null;
                }
                if (series.Count == 0)
                {
                    series = null;
                }
            }
        }
    }
}
