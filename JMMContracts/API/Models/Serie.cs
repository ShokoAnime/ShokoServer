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
    public class Serie
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string AddedAt { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string AirDate { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string AnimeType { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Art Art { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ChapterSource { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ChildCount { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Childrens { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ContentRating { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Duration { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string EpisodeCount { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string EpisodeNumber { get; set; }
        //public string Extras { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ExtraType { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<Tag> Genres { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string GrandparentArt { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string GrandparentKey { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string GrandparentRatingKey { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string GrandparentThumb { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string GrandparentTitle { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Group { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Guid { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Id { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Index { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool IsMovie { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Key { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string LastViewedAt { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string LeafCount { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<Media> Medias { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string OriginallyAvailableAt { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string OriginalTitle { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ParentArt { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ParentIndex { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ParentKey { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ParentRatingKey { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ParentThumb { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ParentTitle { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string PrimaryExtraKey { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Rating { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string RatingKey { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Related { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<RoleTag> roles { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Season { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string SourceTitle { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Studio { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Summary { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Tagline { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<Tag> Tags { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Thumb { get; set; }

        public string Title { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Title1 { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Title2 { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<Title> Titles { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Type { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string UpdatedAt { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Url { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ViewCount { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ViewedLeafCount { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ViewOffset { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Year { get; set; }
        
        /// <summary>
        /// Parametraless constructor for XML Serialization 
        /// </summary>
        public Serie()
        {

        }

        public Serie(PlexAndKodi.Video video)
        {
            AddedAt = video.AddedAt;
            AirDate = video.AirDate.ToString();
            AnimeType = video.AnimeType;
            Art = new Art(video);
            ChapterSource = video.ChapterSource;
            ChildCount = video.ChildCount;
            ContentRating = video.ContentRating;
            Duration = video.Duration;
            EpisodeCount = video.EpisodeCount;
            EpisodeNumber = video.EpisodeNumber;
            
            //Extras = video.Extras;

            ExtraType = video.ExtraType;

            if (video.Genres!= null)
            {
                Genres = new List<Tag>();
                foreach (JMMContracts.PlexAndKodi.Tag tag in video.Genres)
                {
                    Genres.Add(new Tag(tag));
                }
            }
            
            GrandparentArt = video.GrandparentArt;
            GrandparentKey = video.GrandparentKey;
            GrandparentRatingKey = video.GrandparentRatingKey;
            GrandparentThumb = video.GrandparentThumb;
            GrandparentTitle = video.GrandparentTitle;

            //Group = mc.Group;

            Guid = video.Guid;
            Id = video.Id;
            
            Index = video.Index;
            IsMovie = video.IsMovie;
            Key = video.Key;
            LastViewedAt = video.LastViewedAt;
            LeafCount = video.LeafCount;

            if (video.Medias != null)
            {
                Medias = new List<Media>();
                foreach (JMMContracts.PlexAndKodi.Media media in video.Medias)
                {
                    Medias.Add(new Media(media));
                }
            }

            OriginallyAvailableAt = video.OriginallyAvailableAt;
            OriginalTitle = video.OriginalTitle;
            ParentArt = video.ParentArt;
            ParentIndex = video.ParentIndex;
            ParentKey = video.ParentKey;
            ParentRatingKey = video.ParentRatingKey;
            ParentThumb = video.ParentThumb;
            ParentTitle = video.ParentTitle;
            PrimaryExtraKey = video.PrimaryExtraKey;
            Rating = video.Rating;
            RatingKey = video.RatingKey;
            //Related = video.Related;
            if (video.Roles != null)
            {
                roles = new List<RoleTag>();
                foreach (JMMContracts.PlexAndKodi.RoleTag rt in video.Roles)
                {
                    roles.Add(new RoleTag(rt));
                }
            }
            Season = video.Season;
            SourceTitle = video.SourceTitle;
            Studio = video.Studio;
            Summary = video.Summary;
            Tagline = video.Tagline;
            if (video.Tags != null)
            {
                Tags = new List<Tag>();
                foreach (JMMContracts.PlexAndKodi.Tag tag in video.Tags)
                {
                    Tags.Add(new Tag(tag));
                }
            }
            Thumb = video.Thumb;
            Title = video.Title;
            Title1 = video.Title1;
            Title2 = video.Title2;
            if (video.Titles != null)
            {
                Titles = new List<Models.Title>();
                foreach (JMMContracts.PlexAndKodi.AnimeTitle at in video.Titles)
                {
                    Titles.Add(new Models.Title(at));
                }
            }
                        
            Type = video.Type;
            UpdatedAt = video.UpdatedAt;
            Url = video.Url;
            ViewCount = video.ViewCount;
            ViewedLeafCount = video.ViewedLeafCount;
            ViewOffset = video.ViewOffset;
            Year = video.Year;
        }
    }
}

