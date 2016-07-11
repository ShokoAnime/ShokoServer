using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMContracts.API
{
    public class API_MediaContainer
    {
        public List<API_Video> Childrens { get; set; }

        // [Plex]
        //public string ViewGroup { get; set; }
        //public string ViewMode { get; set; }
        //public string ContentType { get; set; }
        //public string MediaTagPrefix { get; set; }
        //public string MediaTagVersion { get; set; }
        //public string AllowSync { get; set; }
        //public string NoCache { get; set; }
        //public string LibrarySectionUUID { get; set; }
        //public string LibrarySectionTitle { get; set; }
        //public string LibrarySectionID { get; set; }


        public string Size { get; set; }
        public string Identifier { get; set; }
        public string TotalSize { get; set; }
        public string Offset { get; set; }
        public string ErrorString { get; set; }

        public static explicit operator API_MediaContainer(JMMContracts.PlexAndKodi.MediaContainer old)
        {
            API_MediaContainer api = new API_MediaContainer();
            foreach (PlexAndKodi.Video video in old.Childrens)
            {
                api.Childrens.Add((API_Video)video);
            }
            api.Size = old.Size;
            api.Identifier = old.Identifier;
            api.TotalSize = old.TotalSize;
            api.Offset = old.Offset;
            api.ErrorString = old.ErrorString;

            return api;
        }
    }

    public class API_Video
    {
        public DateTime AirDate { get; set; }
        public bool IsMovie { get; set; }
        public string Id { get; set; }
        public string AnimeType { get; set; }
        public string Art { get; set; }
        public string Url { get; set; }
        public string Thumb { get; set; }
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
        public string Season { get; set; }
        public string ViewCount { get; set; }
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
        public List<API_Media> Medias { get; set; }
        public List<API_RoleTag> Roles { get; set; }
        public API_Extras Extras { get; set; }
        public List<API_Hub> Related { get; set; }
        public List<API_Tag> Tags { get; set; }
        public List<API_Tag> Genres { get; set; }
        public List<API_AnimeTitle> Titles { get; set; }

        public static explicit operator API_Video(JMMContracts.PlexAndKodi.Video old)
        {
            API_Video api = new API_Video();
            api.AirDate = old.AirDate;
            api.IsMovie = old.IsMovie;
            api.Id = old.Id;
            api.AnimeType = old.AnimeType;
            api.Art = old.Art;
            api.Url = old.Url;
            api.Thumb = old.Thumb;
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
            foreach (PlexAndKodi.Media media in old.Medias)
            {
                api.Medias.Add((API_Media)media);
            }
            foreach (PlexAndKodi.RoleTag role in old.Roles)
            {
                api.Roles.Add((API_RoleTag)role);
            }
            api.Extras = (API_Extras)old.Extras;
            foreach (PlexAndKodi.Hub hub in old.Related)
            {
                api.Related.Add((API_Hub)hub);
            }
            foreach (PlexAndKodi.Tag tag in old.Tags)
            {
                api.Tags.Add((API_Tag)tag);
            }
            foreach (PlexAndKodi.Tag genr in old.Genres)
            {
                api.Genres.Add((API_Tag)genr);
            }
            foreach (PlexAndKodi.AnimeTitle animetitle in old.Titles)
            {
                api.Titles.Add((API_AnimeTitle)animetitle);
            }

            return api;
        }
    }

    public class API_Media
    {
        public List<API_Part> Parts { get; set; }
    
        public string Duration { get; set; }

        public string VideoFrameRate { get; set; }

        public string Container { get; set; }
   
        public string VideoCodec { get; set; }

        public string AudioCodec { get; set; }

        public string AudioChannels { get; set; }

        public string AspectRatio { get; set; }

        public string Height { get; set; }

        public string Width { get; set; }

        public string Bitrate { get; set; }

        public string Id { get; set; }

        public string VideoResolution { get; set; }

        public string OptimizedForStreaming { get; set; }

        public static explicit operator API_Media(JMMContracts.PlexAndKodi.Media old)
        {
            API_Media api = new API_Media();

            foreach (PlexAndKodi.Part parts in old.Parts)
            {
                api.Parts.Add((API_Part)parts);
            }

            api.Duration = old.Duration;

            api.VideoFrameRate = old.VideoFrameRate;

            api.Container = old.Container;

            api.VideoCodec = old.VideoCodec;

            api.AudioCodec = old.AudioCodec;

            api.AudioChannels = old.AudioChannels;

            api.AspectRatio = old.AspectRatio;

            api.Height = old.Height;

            api.Width = old.Width;

            api.Bitrate = old.Bitrate;

            api.Id = old.Id;

            api.VideoResolution = old.VideoResolution;

            api.OptimizedForStreaming = old.OptimizedForStreaming;

            return api;
        }
    }

    public class API_Part
    {
        public string Accessible { get; set; }

        public string Exists { get; set; }

        public List<API_Stream> Streams { get; set; }

        public string Size { get; set; }

        public string Duration { get; set; }

        public string Key { get; set; }

        public string Container { get; set; }

        public string Id { get; set; }

        public string File { get; set; }

        public string OptimizedForStreaming { get; set; }

        public string Extension { get; set; }

        public string Has64bitOffsets { get; set; }

        public static explicit operator API_Part(JMMContracts.PlexAndKodi.Part old)
        {
            API_Part api = new API_Part();
            api.Accessible = old.Accessible;
            api.Exists = old.Exists;
            foreach (PlexAndKodi.Stream stream in old.Streams)
            {
                api.Streams.Add((API_Stream)stream);
            }
            api.Size = old.Size;
            api.Duration = old.Duration;
            api.Key = old.Key;
            api.Container = old.Container;
            api.Id = old.Id;
            api.File = old.File;
            api.OptimizedForStreaming = old.OptimizedForStreaming;
            api.Extension = old.Extension;
            api.Has64bitOffsets = old.Has64bitOffsets;
            return api;
        }
    }

    public class API_Stream
    {
        public string Title { get; set; }

        public string Language { get; set; }

        public string Key { get; set; }

        public string Duration { get; set; }

        public string Height { get; set; }

        public string Width { get; set; }

        public string Bitrate { get; set; }

        public string SubIndex { get; set; }

        public string Id { get; set; }

        public string ScanType { get; set; }

        public string RefFrames { get; set; }

        public string Profile { get; set; }

        public string Level { get; set; }

        public string HeaderStripping { get; set; }
        public string HasScalingMatrix { get; set; }

        public string FrameRateMode { get; set; }

        public string File { get; set; }

        public string FrameRate { get; set; }

        public string ColorSpace { get; set; }

        public string CodecID { get; set; }

        public string ChromaSubsampling { get; set; }

        public string Cabac { get; set; }

        public string BitDepth { get; set; }

        public string Index { get; set; }

        public int idx { get; set; }

        public string Codec { get; set; }

        public string StreamType { get; set; }

        public string Orientation { get; set; }

        public string QPel { get; set; }

        public string GMC { get; set; }

        public string BVOP { get; set; }

        public string SamplingRate { get; set; }

        public string LanguageCode { get; set; }

        public string Channels { get; set; }

        public string Selected { get; set; }

        public string DialogNorm { get; set; }

        public string BitrateMode { get; set; }

        public string Format { get; set; }

        public string Default { get; set; }

        public string Forced { get; set; }

        public string PixelAspectRatio { get; set; }

        public float PA { get; set; }

        public static explicit operator API_Stream(JMMContracts.PlexAndKodi.Stream old)
        {
            API_Stream api = new API_Stream();

            api.Title = old.Title;

            api.Language = old.Language;

            api.Key = old.Key;

            api.Duration = old.Duration;

            api.Height = old.Height;

            api.Width = old.Width;

            api.Bitrate = old.Bitrate;

            api.SubIndex = old.SubIndex;

            api.Id = old.Id;

            api.ScanType = old.ScanType;

            api.RefFrames = old.RefFrames;

            api.Profile = old.Profile;

            api.Level = old.Level;

            api.HeaderStripping = old.HeaderStripping;
            api.HasScalingMatrix = old.HasScalingMatrix;

            api.FrameRateMode = old.FrameRateMode;

            api.File = old.File;

            api.FrameRate = old.FrameRate;

            api.ColorSpace = old.ColorSpace;

            api.CodecID = old.CodecID;

            api.ChromaSubsampling = old.ChromaSubsampling;

            api.Cabac = old.Cabac;

            api.BitDepth = old.BitDepth;

            api.Index = old.Index;

            api.idx = old.idx;

            api.Codec = old.Codec;

            api.StreamType = old.StreamType;

            api.Orientation = old.Orientation;

            api.QPel = old.QPel;

            api.GMC = old.GMC;

            api.BVOP = old.BVOP;

            api.SamplingRate = old.SamplingRate;

            api.LanguageCode = old.LanguageCode;

            api.Channels = old.Channels;

            api.Selected = old.Selected;

            api.DialogNorm = old.DialogNorm;

            api.BitrateMode = old.BitrateMode;

            api.Format = old.Format;

            api.Default = old.Default;

            api.Forced = old.Forced;

            api.PixelAspectRatio = old.PixelAspectRatio;

            api.PA = old.PA;

            return api;
        }
    }

    public class API_RoleTag
    {
        public string Value { get; set; }

        public string Role { get; set; }

        public string RoleDescription { get; set; }

        public string RolePicture { get; set; }

        public string TagPicture { get; set; }

        public static explicit operator API_RoleTag(JMMContracts.PlexAndKodi.RoleTag old)
        {
            API_RoleTag api = new API_RoleTag();
            api.Value = old.Value;
            api.Role = old.Role;
            api.RoleDescription = old.RoleDescription;
            api.RolePicture = old.RolePicture;
            api.TagPicture = old.TagPicture;
            return api;
        }
    }

    public class API_Extras
    {
        public string Size { get; set; }

        public List<API_Video> Videos { get; set; }

        public static explicit operator API_Extras(JMMContracts.PlexAndKodi.Extras old)
        {
            API_Extras api = new API_Extras();
            api.Size = old.Size;
            foreach (PlexAndKodi.Video video in old.Videos)
            {
                api.Videos.Add((API_Video)video);
            }
            return api;
        }
    }

    public class API_Hub
    {
        public string Key { get; set; }

        public string Type { get; set; }

        public string HubIdentifier { get; set; }

        public string Size { get; set; }

        public string Title { get; set; }

        public string More { get; set; }

        public static explicit operator API_Hub(JMMContracts.PlexAndKodi.Hub old)
        {
            API_Hub api = new API_Hub();
            api.Key = old.Key;
            api.Type = old.Type;
            api.HubIdentifier = old.HubIdentifier;
            api.Size = old.Size;
            api.Title = old.Title;
            api.More = old.More;
            return api;
        }
    }

    public class API_Tag
    {
        public string Value { get; set; }
        public string Role { get; set; }

        public static explicit operator API_Tag(JMMContracts.PlexAndKodi.Tag old)
        {
            API_Tag api = new API_Tag();
            api.Value = old.Value;
            api.Role = old.Role;
            return api;
        }
    }

    public class API_AnimeTitle
    {

        public string Type { get; set; }

        public string Language { get; set; }

        public string Title { get; set; }

        public static explicit operator API_AnimeTitle(JMMContracts.PlexAndKodi.AnimeTitle old)
        {
            API_AnimeTitle api = new API_AnimeTitle();
            api.Type = old.Type;
            api.Language = old.Language;
            api.Title = old.Title;
            return api;
        }
    }

    public enum JMMType
    {
        GroupFilter,
        GroupUnsort,
        Group,
        Serie,
        EpisodeType,
        Episode,
        File,
        Playlist,
        FakeIosThumb
    }

    

}
