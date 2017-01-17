using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using Nancy.Rest.Annotations.Atributes;
using Shoko.Models.Client;


namespace Shoko.Models.PlexAndKodi
{
    [XmlType("MediaContainer")]
    [Serializable]
    [DataContract()]
    [KnownType(typeof(Video))]
    [KnownType(typeof(Directory))]
 
    public class MediaContainer : Video
    {

        [XmlElement(typeof(Video), ElementName = "Video")]
        [XmlElement(typeof(Directory), ElementName = "Directory")]
        [DataMember(EmitDefaultValue = false, Order = 1)]
        public List<Video> Childrens { get; set; }

        [Tags("Plex")]
        [XmlAttribute("viewGroup")]
        [DataMember(EmitDefaultValue = false, Order = 2)]
        public string ViewGroup { get; set; }

        [Tags("Plex")]
        [XmlAttribute("viewMode")]
        [DataMember(EmitDefaultValue = false, Order = 3)]
        public string ViewMode { get; set; }

        [Tags("Plex")]
        [DataMember(EmitDefaultValue = false, Order = 4)]
        [XmlAttribute("contenttype")]
        public string ContentType { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 5)]
        [XmlAttribute("size")]
        public string Size { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 6)]
        [XmlAttribute("identifier")]
        public string Identifier { get; set; }

        [Tags("Plex")]
        [DataMember(EmitDefaultValue = false, Order = 7)]
        [XmlAttribute("mediaTagPrefix")]
        public string MediaTagPrefix { get; set; }

        [Tags("Plex")]
        [DataMember(EmitDefaultValue = false, Order = 8)]
        [XmlAttribute("mediaTagVersion")]
        public string MediaTagVersion { get; set; }

        [Tags("Plex")]
        [DataMember(EmitDefaultValue = false, Order = 9)]
        [XmlAttribute("allowSync")]
        public string AllowSync { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 10)]
        [XmlAttribute("totalSize")]
        public string TotalSize { get; set; }

        [Tags("Plex")]
        [DataMember(EmitDefaultValue = false, Order = 11)]
        [XmlAttribute("nocache")]
        public string NoCache { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 12)]
        [XmlAttribute("offset")]
        public string Offset { get; set; }

        [Tags("Plex")]
        [DataMember(EmitDefaultValue = false, Order = 13)]
        [XmlAttribute("librarySectionUUID")]
        public string LibrarySectionUUID { get; set; }

        [Tags("Plex")]
        [DataMember(EmitDefaultValue = false, Order = 14)]
        [XmlAttribute("librarySectionTitle")]
        public string LibrarySectionTitle { get; set; }

        [Tags("Plex")]
        [DataMember(EmitDefaultValue = false, Order = 15)]
        [XmlAttribute("librarySectionID")]
        public string LibrarySectionID { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 19)]
        [XmlAttribute("ErrorString")]
        public string ErrorString { get; set; }
    }

    [Serializable]
    [DataContract]
    public enum AnimeTypes
    {
        [EnumMember]
        MediaContainer,
        [EnumMember]
        AnimeGroup,
        [EnumMember]
        AnimeSerie,
        [EnumMember]
        AnimeEpisode,
        [EnumMember]
        AnimeFile,
        [EnumMember]
        AnimeGroupFilter,
        [EnumMember]
        AnimePlaylist,
        [EnumMember]
        AnimeUnsort
    }

    [Serializable]
    [DataContract]
    public class AnimeTitle
    {
        [DataMember(EmitDefaultValue = false, Order = 1)]
        [XmlElement("Type")]
        public string Type { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 2)]
        [XmlElement("Language")]
        public string Language { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 3)]
        [XmlElement("Title")]
        public string Title { get; set; }
    }


    [XmlRoot(ElementName = "Directory")]
    [Serializable]
    [DataContract]
    public class Directory : Video
    {
    }

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
        [DataMember(EmitDefaultValue = false, Order = 20)]
        public string Id { get; set; }



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

        [DataMember(EmitDefaultValue = false, Order = 36)]
        [XmlAttribute("index")]
        public string Index { get; set; }

	    [Tags("Plex")]
        [DataMember(EmitDefaultValue = false, Order = 37)]
        [XmlAttribute("parentIndex")]
        public string ParentIndex { get; set; }

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

        [DataMember(EmitDefaultValue = false, Order = 46)]
        [XmlAttribute("year")]
        public string Year { get; set; }

	    [Tags("Plex")]
        [DataMember(EmitDefaultValue = false, Order = 47)]
        [XmlAttribute("duration")]
        public string Duration { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 48)]
        [XmlAttribute("episode_count")]
        public string EpisodeCount { get; set; }

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

        [DataMember(EmitDefaultValue = false, Order = 53)]
        [XmlAttribute("leafCount")]
        public string LeafCount { get; set; }

	    [Tags("Plex")]
        [DataMember(EmitDefaultValue = false, Order = 54)]
        [XmlAttribute("childCount")]
        public string ChildCount { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 55)]
        [XmlAttribute("viewedLeafCount")]
        public string ViewedLeafCount { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 56)]
        [XmlAttribute("original_title")]
        public string OriginalTitle { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 57)]
        [XmlAttribute("source_title")]
        public string SourceTitle { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 58)]
        [XmlAttribute("rating")]
        public string Rating { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 59)]
        [XmlAttribute("season")]
        public string Season { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 60)]
        [XmlAttribute("viewCount")]
        public string ViewCount { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 61)]
        [XmlAttribute("viewOffset")]
        public string ViewOffset { get; set; }

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

        [DataMember(EmitDefaultValue = false, Order = 68)]
        [XmlAttribute]
        public string EpisodeType { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 69)]
        [XmlAttribute]
        public string EpisodeNumber { get; set; }

        [XmlIgnore]
        [Ignore]
        public Client.CL_AnimeGroup_User Group { get; set; }

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

    [Serializable]
    [DataContract]
    public class Contract_ImageDetails
    {
        [XmlAttribute("ID")]
        [DataMember(EmitDefaultValue = false, Order = 1)]
        public int ImageID { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 2)]
        [XmlAttribute("Type")]
        public int ImageType { get; set; }
    }

    [Serializable]
    [DataContract]
    public class RoleTag
    {
        [XmlAttribute("tag")]
        [DataMember(EmitDefaultValue = false, Order = 1)]
        public string Value { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 2)]
        [XmlAttribute("role")]
        public string Role { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 3)]
        [XmlAttribute("roleDescription")]
        public string RoleDescription { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 4)]
        [XmlAttribute("rolePicture")]
        public string RolePicture { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 5)]
        [XmlAttribute("tagPicture")]
        public string TagPicture { get; set; }
    }

    [Serializable]
    [DataContract]
    public class Response
    {
        [DataMember(EmitDefaultValue = false, Order = 1)]
        [XmlAttribute("Code")]
        public string Code { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 2)]
        [XmlAttribute("Message")]
        public string Message { get; set; }
    }

    [Serializable]
    [DataContract]
    public class Tag
    {
        [DataMember(EmitDefaultValue = false, Order = 1)]
        [XmlAttribute("tag")]
        public string Value { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 2)]
        [XmlAttribute("role")]
        public string Role { get; set; }
		// Override for ease of making sets
	    protected bool Equals(Tag other)
	    {
		    return string.Equals(Value, other.Value) && string.Equals(Role, other.Role);
	    }

	    public override bool Equals(object obj)
	    {
		    if (ReferenceEquals(null, obj)) return false;
		    if (ReferenceEquals(this, obj)) return true;
		    if (obj.GetType() != this.GetType()) return false;
		    return Equals((Tag) obj);
	    }

	    public override int GetHashCode()
	    {
		    unchecked
		    {
			    return ((Value != null ? Value.GetHashCode() : 0) * 397) ^ (Role != null ? Role.GetHashCode() : 0);
		    }
	    }
    }

    [XmlType("Media")]
    [DataContract]
    [Serializable]
    public class Media
    {
        [DataMember(EmitDefaultValue = false, Order = 1)]
        [XmlElement("Part")]
        public List<Part> Parts { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 2)]
        [XmlAttribute("duration")]
        public string Duration { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 3)]
        [XmlAttribute("videoFrameRate")]
        public string VideoFrameRate { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 4)]
        [XmlAttribute("container")]
        public string Container { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 5)]
        [XmlAttribute("videoCodec")]
        public string VideoCodec { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 6)]
        [XmlAttribute("audioCodec")]
        public string AudioCodec { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 7)]
        [XmlAttribute("audioChannels")]
        public string AudioChannels { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 8)]
        [XmlAttribute("aspectRatio")]
        public string AspectRatio { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 9)]
        [XmlAttribute("height")]
        public string Height { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 10)]
        [XmlAttribute("width")]
        public string Width { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 11)]
        [XmlAttribute("bitrate")]
        public string Bitrate { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 12)]
        [XmlAttribute("id")]
        public string Id { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 13)]
        [XmlAttribute("videoResolution")]
        public string VideoResolution { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 14)]
        [XmlAttribute("optimizedForStreaming")]
        public string OptimizedForStreaming { get; set; }
    }

    [Serializable]
    [XmlType("Part")]
    [DataContract]
    public class Part
    {
        [DataMember(EmitDefaultValue = false, Order = 1)]
        [XmlAttribute("accessible")]
        public string Accessible { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 2)]
        [XmlAttribute("exists")]
        public string Exists { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 3)]
        [XmlElement("Stream")]
        public List<Stream> Streams { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 4)]
        [XmlAttribute("size")]
        public string Size { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 5)]
        [XmlAttribute("duration")]
        public string Duration { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 6)]
        [XmlAttribute("key")]
        public string Key { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 7)]
        [XmlAttribute("local_key")]
        public string LocalKey { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 8)]
        [XmlAttribute("container")]
        public string Container { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 9)]
        [XmlAttribute("id")]
        public string Id { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 10)]
        [XmlAttribute("file")]
        public string File { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 11)]
        [XmlAttribute("optimizedForStreaming")]
        public string OptimizedForStreaming { get; set; }

        [Ignore]
        [XmlIgnore]
        public string Extension { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 12)]
        [XmlAttribute("has64bitOffsets")]
        public string Has64bitOffsets { get; set; }
    }

    [XmlType("Stream")]
    [DataContract]
    [Serializable]
    public class Stream
    {
        [DataMember(EmitDefaultValue = false, Order = 1)]
        [XmlAttribute("title")]
        public string Title { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 2)]
        [XmlAttribute("language")]
        public string Language { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 3)]
        [XmlAttribute("key")]
        public string Key { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 4)]
        [XmlAttribute("duration")]
        public string Duration { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 5)]
        [XmlAttribute("height")]
        public string Height { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 6)]
        [XmlAttribute("width")]
        public string Width { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 7)]
        [XmlAttribute("bitrate")]
        public string Bitrate { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 8)]
        [XmlAttribute("subIndex")]
        public string SubIndex { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 9)]
        [XmlAttribute("id")]
        public string Id { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 10)]
        [XmlAttribute("scanType")]
        public string ScanType { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 11)]
        [XmlAttribute("refFrames")]
        public string RefFrames { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 12)]
        [XmlAttribute("profile")]
        public string Profile { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 13)]
        [XmlAttribute("level")]
        public string Level { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 14)]
        [XmlAttribute("headerStripping")]
        public string HeaderStripping { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 15)]
        [XmlAttribute("hasScalingMatrix")]
        public string HasScalingMatrix { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 16)]
        [XmlAttribute("frameRateMode")]
        public string FrameRateMode { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 17)]
        [XmlAttribute("file")]
        public string File { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 18)]
        [XmlAttribute("frameRate")]
        public string FrameRate { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 19)]
        [XmlAttribute("colorSpace")]
        public string ColorSpace { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 20)]
        [XmlAttribute("codecID")]
        public string CodecID { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 21)]
        [XmlAttribute("chromaSubsampling")]
        public string ChromaSubsampling { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 22)]
        [XmlAttribute("cabac")]
        public string Cabac { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 23)]
        [XmlAttribute("bitDepth")]
        public string BitDepth { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 24)]
        [XmlAttribute("index")]
        public string Index { get; set; }

        [XmlIgnore]
        [Ignore]
        public int idx;

        [DataMember(EmitDefaultValue = false, Order = 25)]
        [XmlAttribute("codec")]
        public string Codec { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 26)]
        [XmlAttribute("streamType")]
        public string StreamType { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 27)]
        [XmlAttribute("orientation")]
        public string Orientation { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 28)]
        [XmlAttribute("qpel")]
        public string QPel { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 29)]
        [XmlAttribute("gmc")]
        public string GMC { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 30)]
        [XmlAttribute("bvop")]
        public string BVOP { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 31)]
        [XmlAttribute("samplingRate")]
        public string SamplingRate { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 32)]
        [XmlAttribute("languageCode")]
        public string LanguageCode { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 33)]
        [XmlAttribute("channels")]
        public string Channels { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 34)]
        [XmlAttribute("selected")]
        public string Selected { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 35)]
        [XmlAttribute("dialogNorm")]
        public string DialogNorm { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 36)]
        [XmlAttribute("bitrateMode")]
        public string BitrateMode { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 37)]
        [XmlAttribute("format")]
        public string Format { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 38)]
        [XmlAttribute("default")]
        public string Default { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 39)]
        [XmlAttribute("forced")]
        public string Forced { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 40)]
        [XmlAttribute("pixelAspectRatio")]
        public string PixelAspectRatio { get; set; }

        [XmlIgnore]
        [Ignore]
        public float PA { get; set; }
    }

    [DataContract]
    [Serializable]
    [XmlType("User")]
    public class PlexContract_User
    {
        [XmlAttribute("id")]
        [DataMember(EmitDefaultValue = false, Order = 1)]
        public string id { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 2)]
        [XmlAttribute("name")]
        public string name { get; set; }
    }

    [DataContract]
    [Serializable]
    [XmlType("Users")]
    public class PlexContract_Users
    {
        [DataMember(EmitDefaultValue = false, Order = 1)]
        [XmlElement("User")]
        public List<PlexContract_User> Users { get; set; }
        [DataMember(EmitDefaultValue = false, Order = 2)]
        [XmlAttribute("ErrorString")]
        public string ErrorString { get; set; }
    }

    [XmlType("Extras")]
    [Serializable]
    [DataContract]
    public class Extras
    {
        [DataMember(EmitDefaultValue = false, Order = 1)]
        [XmlAttribute("size")]
        public string Size { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 2)]
        [XmlElement("Video")]
        public List<Video> Videos { get; set; }
    }

    [XmlType("Hub")]
    [Serializable]
    [DataContract]
    public class Hub
    {
        [DataMember(EmitDefaultValue = false, Order = 1)]
        [XmlAttribute("key")]
        public string Key { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 2)]
        [XmlAttribute("type")]
        public string Type { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 3)]
        [XmlAttribute("hubIdentifier")]
        public string HubIdentifier { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 4)]
        [XmlAttribute("size")]
        public string Size { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 5)]
        [XmlAttribute("title")]
        public string Title { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 6)]
        [XmlAttribute("more")]
        public string More { get; set; }
    }

    [DataContract]
    public enum JMMType
    {
        [EnumMember]
        GroupFilter,
        [EnumMember]
        GroupUnsort,
        [EnumMember]
        Group,
        [EnumMember]
        Serie,
        [EnumMember]
        EpisodeType,
        [EnumMember]
        Episode,
        [EnumMember]
        File,
        [EnumMember]
        Playlist,
        [EnumMember]
        FakeIosThumb
    }


}