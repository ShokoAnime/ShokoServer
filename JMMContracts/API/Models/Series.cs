using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMContracts.API.Models
{
    /// <summary>
    /// Model that represents Series
    /// </summary>
    public class Series
    {
        /// <summary>
        /// List of series
        /// </summary>
        public List<Serie> series { get; set; }
        
        /// <summary>
        /// List of videos
        /// </summary>
        public List<Video> videos { get; set; }

        /// <summary>
        /// Error String
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ErrorString { get; set; }

        public string Identifier { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string LibrarySectionID { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string LibrarySectionTitle { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string LibrarySectionUUID { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string MediaTagPrefix  { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string MediaTagVersion { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string NoCache  { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Offset  { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Size { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string TotalSize { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ViewGroup  { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ViewMode { get; set; }

        public string Title { get; set; }

        /// <summary>
        /// Parametraless constructor for XML Serialization 
        /// </summary>
        public Series()
        {
            
        }

        /// <summary>
        /// Construktor creating Series out of MediaContainer
        /// </summary>
        /// <param name="mc"></param>
        public Series(PlexAndKodi.MediaContainer mc)
        {
            ErrorString = mc.ErrorString;
            Identifier = mc.Identifier;
            LibrarySectionID = mc.LibrarySectionID;
            LibrarySectionTitle = mc.LibrarySectionTitle;
            LibrarySectionUUID = mc.LibrarySectionUUID;
            MediaTagPrefix = mc.MediaTagPrefix;
            MediaTagVersion = mc.MediaTagVersion;
            NoCache = mc.NoCache;
            Offset = mc.Offset;
            Size = mc.Size;
            TotalSize = mc.TotalSize;
            ViewGroup = mc.ViewGroup;
            ViewMode = mc.ViewMode;
            Title = mc.Title1;

            if (mc.Childrens != null)
            {
                series = new List<Serie>();
                foreach (PlexAndKodi.Video video in mc.Childrens)
                {
                    series.Add(new Serie(video));
                }
            }
        }
    }
}
