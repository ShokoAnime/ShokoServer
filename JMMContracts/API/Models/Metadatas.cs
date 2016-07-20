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
        /// Hold video list
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<JMMContracts.API.Models.Tag> genres { get; set; }

        /// <summary>
        /// Hold video list
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<JMMContracts.API.Models.RoleTag> roles { get; set; }

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
                switch (type)
                {
                    //series detail info
                    case "3":
                        videos = new List<Video>();
                        genres = new List<Tag>();
                        roles = new List<RoleTag>();
                        foreach (PlexAndKodi.Video video in mc.Childrens)
                        {
                            videos.Add(new Video(video));
                            if (genres.Count == 0)
                            {
                                if (video.Genres != null)
                                {
                                    foreach (Tag tag in video.Genres)
                                    {
                                        genres.Add(tag);
                                    }
                                }
                            }
                            if (roles.Count == 0 )
                            {
                                if (video.Roles != null)
                                {
                                    foreach (RoleTag tag in video.Roles)
                                    {
                                        roles.Add(tag);
                                    }
                                }
                            }
                        }
                        break;
                    //series
                    case "0":
                    //series that are included in filter
                    case "5":
                        series = new List<Serie>();
                        foreach (PlexAndKodi.Video video in mc.Childrens)
                        {
                            series.Add(new Serie(video));
                        }
                        break;

                    default:
                        metadatas = new List<Metadata>();
                        foreach (PlexAndKodi.Video video in mc.Childrens)
                        {
                            metadatas.Add(new Metadata(video));
                        }
                        break;
                }
            }
        }
    }
}
