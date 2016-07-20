using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMContracts.API.Models
{
    public class Video
    {
        public DateTime AirDate { get; set; }

        public bool IsMovie { get; set; }

        public string Id { get; set; }

        public string AnimeType { get; set; }

        public Art Arts { get; set; }

        public string Url { get; set; }

        public string ParentThumb { get; set; }

        public string GrandparentThumb { get; set; }

        public string ParentArt { get; set; }

        public string GrandparentArt { get; set; }

        public string RatingKey { get; set; }

        public string ParentRatingKey { get; set; }

        public string GrandparentRatingKey { get; set; }

        public string Key { get; set; }

        public string ParentKey { get; set; }

        public string GrandparentKey { get; set; }

        public string Index { get; set; }

        public string ParentIndex { get; set; }

        public string Guid { get; set; }

        public string Type { get; set; }

        public string Title { get; set; }

        public string Title1 { get; set; }

        public string Title2 { get; set; }

        public string ParentTitle { get; set; }

        public string GrandparentTitle { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Summary { get; set; }

        public string Year { get; set; }

        public string Duration { get; set; }

        public string EpisodeCount { get; set; }

        public string UpdatedAt { get; set; }

        public string AddedAt { get; set; }

        public string LastViewedAt { get; set; }

        public string OriginallyAvailableAt { get; set; }

        public string LeafCount { get; set; }

        public string ChildCount { get; set; }

        public string ViewedLeafCount { get; set; }

        public string OriginalTitle { get; set; }

        public string SourceTitle { get; set; }

        public string Rating { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Season { get; set; }

        public string ViewCount { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ViewOffset { get; set; }

        public string PrimaryExtraKey { get; set; }

        public string ChapterSource { get; set; }

        public string Tagline { get; set; }

        public string ContentRating { get; set; }

        public string Studio { get; set; }

        public string ExtraType { get; set; }

        public string EpisodeType { get; set; }

        public string EpisodeNumber { get; set; }

        public Contract_AnimeGroup Group { get; set; }

        public List<Media> Medias { get; set; }

        // public List<RoleTag> Roles { get; set; }

        public Extras Extras { get; set; }

        public List<Hub> Related { get; set; }

        // public List<Tag> Tags { get; set; }

        // public List<Tag> Genres { get; set; }

        public List<Title> Titles { get; set; }

        public Video()
        {

        }

        public Video(JMMContracts.PlexAndKodi.Video video)
        {
            AirDate = video.AirDate;

            IsMovie = video.IsMovie;

            Id = video.Id;

            AnimeType = video.AnimeType;

            if (video.Art != null || video.Thumb !=null)
            {
                Arts = new Art();
                Arts.fanart = video.Art;
                Arts.thumb = video.Thumb;
            }
          
            Url = video.Url;         

            ParentThumb = video.ParentThumb;

            GrandparentThumb = video.GrandparentThumb;

            ParentArt = video.ParentArt;

            GrandparentArt = video.GrandparentArt;

            RatingKey = video.RatingKey;

            ParentRatingKey = video.ParentRatingKey;

            GrandparentRatingKey = video.GrandparentRatingKey;

            Key = video.Key;

            ParentKey = video.ParentKey;

            GrandparentKey = video.GrandparentKey;

            Index = video.Index;

            ParentIndex = video.ParentIndex;

            Guid = video.Guid;

            Type = video.Type;

            Title = video.Title;

            Title1 = video.Title1;

            Title2 = video.Title2;

            ParentTitle = video.ParentTitle;

            GrandparentTitle = video.GrandparentTitle;

            Summary = video.Summary;

            Year = video.Year;

            Duration = video.Duration;

            EpisodeCount = video.EpisodeCount;

            UpdatedAt = video.UpdatedAt;

            AddedAt = video.AddedAt;

            LastViewedAt = video.LastViewedAt;

            OriginallyAvailableAt = video.OriginallyAvailableAt;

            LeafCount = video.LeafCount;

            ChildCount = video.ChildCount;

            ViewedLeafCount = video.ViewedLeafCount;

            OriginalTitle = video.OriginalTitle;

            SourceTitle = video.SourceTitle;

            Rating = video.Rating;

            Season = video.Season;

            ViewCount = video.ViewCount;

            ViewOffset = video.ViewOffset;

            PrimaryExtraKey = video.PrimaryExtraKey;

            ChapterSource = video.ChapterSource;

            Tagline = video.Tagline;

            ContentRating = video.ContentRating;

            Studio = video.Studio;

            ExtraType = video.ExtraType;

            EpisodeType = video.EpisodeType;

            EpisodeNumber = video.EpisodeNumber;

            Group = video.Group;

            if (video.Medias != null)
            {
                Medias = new List<Media>();
                foreach (JMMContracts.PlexAndKodi.Media media in video.Medias)
                {
                    Medias.Add((Media)media);
                }
            }

            //if (video.Roles != null)
            //{
            //    Roles = new List<RoleTag>();
            //    foreach (JMMContracts.PlexAndKodi.RoleTag role in video.Roles)
            //    {
            //        Roles.Add((RoleTag)role);
            //    }
            //}

            if (video.Extras != null)
            {
                Extras = (Extras)video.Extras;
            }

            if (video.Related != null)
            {
                Related = new List<Hub>();
                foreach (JMMContracts.PlexAndKodi.Hub hub in video.Related)
                {
                    Related.Add((Hub)hub);
                }
            }

            //if (video.Tags != null)
            //{
            //    Tags = new List<Tag>();
            //    foreach (JMMContracts.PlexAndKodi.Tag tag in video.Tags)
            //    {
            //        Tags.Add((Tag)tag);
            //    }
            //}

            //if (video.Genres != null)
            //{
            //    Genres = new List<Tag>();
            //    foreach (JMMContracts.PlexAndKodi.Tag tag in video.Genres)
            //    {
            //        Genres.Add((Tag)tag);
            //    }
            //}

            if (video.Titles != null)
            {
                Titles = new List<Title>();
                foreach (PlexAndKodi.AnimeTitle animetitle in video.Titles)
                {
                    Titles.Add((Title)animetitle);
                }
            }
        }

        public static explicit operator Video(JMMContracts.PlexAndKodi.Video old)
        {
            Video api = new Video();
            api.AirDate = old.AirDate;
            api.IsMovie = old.IsMovie;
            api.Id = old.Id;
            api.AnimeType = old.AnimeType;
            if (old.Art != null || old.Thumb != null)
            {
                api.Arts = new Art();
                api.Arts.fanart = old.Art;
                api.Arts.thumb = old.Thumb;
            }
            api.Url = old.Url;
            api.ParentThumb = old.ParentThumb;
            api.GrandparentThumb = old.GrandparentThumb;
            api.ParentArt = old.ParentArt;
            api.GrandparentArt = old.GrandparentArt;
            api.RatingKey = old.RatingKey;
            api.ParentRatingKey = old.ParentRatingKey;
            api.GrandparentRatingKey = old.GrandparentRatingKey;
            api.Key = old.Key;
            api.ParentKey = old.ParentKey;
            api.GrandparentKey = old.GrandparentKey;
            api.Index = old.Index;
            api.ParentIndex = old.ParentIndex;
            api.Guid = old.Guid;
            api.Type = old.Type;
            api.Title = old.Title;
            api.Title1 = old.Title1;
            api.Title2 = old.Title2;
            api.ParentTitle = old.ParentTitle;
            api.GrandparentTitle = old.GrandparentTitle;
            api.Summary = old.Summary;
            api.Year = old.Year;
            api.Duration = old.Duration;
            api.EpisodeCount = old.EpisodeCount;
            api.UpdatedAt = old.UpdatedAt;
            api.AddedAt = old.AddedAt;
            api.LastViewedAt = old.LastViewedAt;
            api.OriginallyAvailableAt = old.OriginallyAvailableAt;
            api.LeafCount = old.LeafCount;
            api.ChildCount = old.ChildCount;
            api.ViewedLeafCount = old.ViewedLeafCount;
            api.OriginalTitle = old.OriginalTitle;
            api.SourceTitle = old.SourceTitle;
            api.Rating = old.Rating;
            api.Season = old.Season;
            api.ViewCount = old.ViewCount;
            api.ViewOffset = old.ViewOffset;
            api.PrimaryExtraKey = old.PrimaryExtraKey;
            api.ChapterSource = old.ChapterSource;
            api.Tagline = old.Tagline;
            api.ContentRating = old.ContentRating;
            api.Studio = old.Studio;
            api.ExtraType = old.ExtraType;
            api.EpisodeType = old.EpisodeType;
            api.EpisodeNumber = old.EpisodeNumber;
            api.Group = old.Group;

            if (old.Extras != null)
            {
                api.Extras = (Extras)old.Extras;
            }

            if (old.Medias != null)
            {
                api.Medias = new List<Media>();
                foreach (PlexAndKodi.Media media in old.Medias)
                {
                    api.Medias.Add((Media)media);
                }
            }

            //if (old.Roles != null)
            //{
            //    api.Roles = new List<RoleTag>();
            //    foreach (PlexAndKodi.RoleTag role in old.Roles)
            //    {
            //        api.Roles.Add((RoleTag)role);
            //    }
            //}

            if (old.Related != null)
            {
                api.Related = new List<Hub>();
                foreach (PlexAndKodi.Hub hub in old.Related)
                {
                    api.Related.Add((Hub)hub);
                }
            }

            //if (old.Tags != null)
            //{
            //    api.Tags = new List<Tag>();
            //    foreach (PlexAndKodi.Tag tag in old.Tags)
            //    {
            //        api.Tags.Add((Tag)tag);
            //    }
            //}

            //if (old.Genres != null)
            //{
            //    api.Genres = new List<Tag>();
            //    foreach (PlexAndKodi.Tag genr in old.Genres)
            //    {
            //        api.Genres.Add((Tag)genr);
            //    }
            //}

            if (old.Titles != null)
            {
                api.Titles = new List<Title>();
                foreach (PlexAndKodi.AnimeTitle animetitle in old.Titles)
                {
                    api.Titles.Add((Title)animetitle);
                }
            }

            return api;
        }
    }
}
