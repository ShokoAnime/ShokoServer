using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using Nancy.Rest.Annotations.Atributes;
using Shoko.Models.Client;

namespace Shoko.Models.PlexAndKodi
{
    [XmlType("Video")]
    [Serializable]
    [DataContract]

    public class Video
    {
        [XmlIgnore]
        [Ignore]
        public DateTime AirDate { get; set; }

        [XmlIgnore]
        [Ignore]
        public bool IsMovie { get; set; }

        [XmlAttribute("GenericId")]
        [DataMember(EmitDefaultValue = true, Order = 20)]
        public int Id { get; set; }



        [DataMember(EmitDefaultValue = false, Order = 21)]
        [XmlAttribute("AnimeType")]
        public string AnimeType { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 22)]
        [XmlAttribute("art")]
        public string Art { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 23)]
        [XmlAttribute("url")]
        public string Url { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 24)]
        [XmlAttribute("thumb")]
        public string Thumb { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 25)]
        [XmlAttribute("banner")]
        public string Banner { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 26)]
        [XmlAttribute("parentThumb")]
        public string ParentThumb { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 27)]
        [XmlAttribute("grandparentThumb")]
        public string GrandparentThumb { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 28)]
        [XmlAttribute("parentArt")]
        public string ParentArt { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 29)]
        [XmlAttribute("grandparentArt")]
        public string GrandparentArt { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 30)]
        [XmlAttribute("ratingKey")]
        public string RatingKey { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 31)]
        [XmlAttribute("parentRatingkey")]
        public string ParentRatingKey { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 32)]
        [XmlAttribute("grandparentRatingKey")]
        public string GrandparentRatingKey { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 33)]
        [XmlAttribute("key")]
        public string Key { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 34)]
        [XmlAttribute("parentKey")]
        public string ParentKey { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 35)]
        [XmlAttribute("grandparentKey")]
        public string GrandparentKey { get; set; }

        [DataMember(EmitDefaultValue = true, Order = 36)]
        [XmlAttribute("index")]
        public int Index { get; set; }

        [Tags("Plex")]
        [DataMember(EmitDefaultValue = true, Order = 37)]
        [XmlAttribute("parentIndex")]
        public int ParentIndex { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 38)]
        [XmlAttribute("guid")]
        public string Guid { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 39)]
        [XmlAttribute("type")]
        public string Type { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 40)]
        [XmlAttribute("title")]
        public string Title { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 41)]
        [XmlAttribute("title1")]
        public string Title1 { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 42)]
        [XmlAttribute("title2")]
        public string Title2 { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 43)]
        [XmlAttribute("parentTitle")]
        public string ParentTitle { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 44)]
        [XmlAttribute("grandparentTitle")]
        public string GrandparentTitle { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 45)]
        [XmlAttribute("summary")]
        public string Summary { get; set; }

        [DataMember(EmitDefaultValue = true, Order = 46)]
        [XmlAttribute("year")]
        public int Year { get; set; }

        [Tags("Plex")]
        [DataMember(EmitDefaultValue = true, Order = 47)]
        [XmlAttribute("duration")]
        public long Duration { get; set; }

        [DataMember(EmitDefaultValue = true, Order = 48)]
        [XmlAttribute("episode_count")]
        public int EpisodeCount { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 49)]
        [XmlAttribute("updatedAt")]
        public string UpdatedAt { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 50)]
        [XmlAttribute("addedAt")]
        public string AddedAt { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 51)]
        [XmlAttribute("lastViewedAt")]
        public string LastViewedAt { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 52)]
        [XmlAttribute("originallyAvailableAt")]
        public string OriginallyAvailableAt { get; set; }

        [DataMember(EmitDefaultValue = true, Order = 53)]
        [XmlAttribute("leafCount")]
        public int LeafCount { get; set; }

        [Tags("Plex")]
        [DataMember(EmitDefaultValue = true, Order = 54)]
        [XmlAttribute("childCount")]
        public int ChildCount { get; set; }

        [DataMember(EmitDefaultValue = true, Order = 55)]
        [XmlAttribute("viewedLeafCount")]
        public int ViewedLeafCount { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 56)]
        [XmlAttribute("original_title")]
        public string OriginalTitle { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 57)]
        [XmlAttribute("source_title")]
        public string SourceTitle { get; set; }

        [DataMember(EmitDefaultValue = true, Order = 58)]
        [XmlAttribute("rating")]
        public int Rating { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 59)]
        [XmlAttribute("season")]
        public string Season { get; set; }

        [DataMember(EmitDefaultValue = true, Order = 60)]
        [XmlAttribute("viewCount")]
        public int ViewCount { get; set; }

        [DataMember(EmitDefaultValue = true, Order = 61)]
        [XmlAttribute("viewOffset")]
        public long ViewOffset { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 62)]
        [XmlAttribute("primaryExtraKey")]
        public string PrimaryExtraKey { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 63)]
        [XmlAttribute("chapterSource")]
        public string ChapterSource { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 64)]
        [XmlAttribute("tagline")]
        public string Tagline { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 65)]
        [XmlAttribute("contentRating")]
        public string ContentRating { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 66)]
        [XmlAttribute("studio")]
        public string Studio { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 67)]
        [XmlAttribute("extraType")]
        public string ExtraType { get; set; }

        [DataMember(EmitDefaultValue = true, Order = 68)]
        [XmlAttribute]
        public int EpisodeType { get; set; }

        [DataMember(EmitDefaultValue = true, Order = 69)]
        [XmlAttribute]
        public int EpisodeNumber { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 70)]
        [XmlAttribute("UserRating")]
        public int UserRating { get; set; }

        [XmlIgnore]
        [Ignore]
        public CL_AnimeGroup_User Group { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 100)]
        [XmlElement("Media")]
        public List<Media> Medias { get; set; }

        [Tags("Role")]
        [DataMember(EmitDefaultValue = false, Order = 101)]
        [XmlElement("Role")]
        public List<RoleTag> Roles { get; set; }

        [Tags("Extras")]
        [DataMember(EmitDefaultValue = false, Order = 102)]
        [XmlElement("Extras")]
        public Extras Extras { get; set; }

        [Tags("Related")]
        [DataMember(EmitDefaultValue = false, Order = 103)]
        [XmlElement("Related")]
        public List<Hub> Related { get; set; }

        [Tags("Tags")]
        [DataMember(EmitDefaultValue = false, Order = 104)]
        [XmlElement("Tag")]
        public List<Tag> Tags { get; set; }

        [Tags("Genres")]
        [DataMember(EmitDefaultValue = false, Order = 105)]
        [XmlElement("Genre")]
        public List<Tag> Genres { get; set; }

        [Tags("Titles")]
        [DataMember(EmitDefaultValue = false, Order = 106)]
        [XmlElement("AnimeTitle")]
        public List<AnimeTitle> Titles { get; set; }

        [Tags("Fanarts")]
        [DataMember(EmitDefaultValue = false, Order = 107)]
        [XmlElement("Fanarts")]
        public List<Contract_ImageDetails> Fanarts { get; set; }

        [Tags("Banners")]
        [DataMember(EmitDefaultValue = false, Order = 108)]
        [XmlElement("Banners")]
        public List<Contract_ImageDetails> Banners { get; set; }

        [XmlAttribute("prompt")]
        [DataMember(EmitDefaultValue = false, Order = 109)]
        public string Prompt { get; set; }

        [XmlAttribute("search")]
        [DataMember(EmitDefaultValue = false, Order = 110)]
        public string Search { get; set; }

        [XmlAttribute("settings")]
        [DataMember(EmitDefaultValue = false, Order = 111)]
        public string Settings { get; set; }
    }
}