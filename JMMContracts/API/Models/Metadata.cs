using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMContracts.API.Models
{
    /// <summary>
    /// Model that represends MetaData of Episode
    /// </summary>
    public class Metadata
    {
        /// <summary>
        /// ID uniq for same type
        /// </summary>
        public string id { get; set; }

        /// <summary>
        /// title
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string title { get; set; }

        /// <summary>
        /// Episode number
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string episode { get; set; }

        /// <summary>
        /// Season number
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string season { get; set; }

        /// <summary>
        /// Rating from Community pages
        /// </summary>
        public string rating { get; set; }

        /// <summary>
        /// Data of first air
        /// </summary>
        public string datastart { get; set; }

        /// <summary>
        /// Plot
        /// </summary>
        public string plot { get; set; }

        /// <summary>
        /// Data of adding to (source? or collection?)
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string dateadded { get; set; }

        /// <summary>
        /// key / url to item
        /// </summary>
        public string key { get; set; }

        /// <summary>
        /// Art holder
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Art art { get; set; }

        /// <summary>
        /// Watched status
        /// </summary>
        public string watched { get; set; }

        /// <summary>
        /// Media content
        /// </summary>
        public List<Media> media { get; set; }

        /// <summary>
        /// Tags
        /// </summary>
        public List<Tag> tags { get; set; }

        /// <summary>
        /// RoleTags
        /// </summary>
        public List<RoleTag> cast { get; set; }

        /// <summary>
        /// Genre
        /// </summary>
        public List<Tag> genre { get; set; }

        /// <summary>
        /// Year
        /// </summary>
        public string year { get; set; }

        /// <summary>
        /// Parametraless constructor for XML Serialization
        /// </summary>
        public Metadata()
        {

        }

        /// <summary>
        /// Contructor that create Metadata_Episode out of video
        /// </summary>
        public Metadata(PlexAndKodi.Video video)
        {
            id = video.Id;
            title = video.Title;
            episode = video.EpisodeNumber;
            season = video.Season;
            rating = video.Rating;
            //votes
            //my_rating
            datastart = video.OriginallyAvailableAt;
            //datastop
            plot = video.Summary;
            //moe.status = video
            dateadded = video.AddedAt;
            key = video.Key;
            art = new Art(video);
            //source?
            //moe.source = video.SourceTitle;
            watched = video.ViewedLeafCount;

            if (video.Medias != null)
            {
                media = new List<Media>();
                foreach (JMMContracts.PlexAndKodi.Media md in video.Medias)
                {
                    media.Add(new Media(md));
                }
            }

            if (video.Tags != null)
            {
                tags = new List<Tag>();
                foreach (JMMContracts.PlexAndKodi.Tag tag in video.Tags)
                {
                    tags.Add(new Tag(tag));
                }
            }

            if (video.Roles != null)
            {
                cast = new List<RoleTag>();
                foreach (JMMContracts.PlexAndKodi.RoleTag roletag in video.Roles)
                {
                    cast.Add(new RoleTag(roletag));
                }
            }

            if (video.Genres != null)
            {
                genre = new List<Tag>();
                foreach (JMMContracts.PlexAndKodi.Tag gen in video.Genres)
                {
                    genre.Add(new Tag(gen));
                }
            }

            year = video.Year;
        }
    }
}
