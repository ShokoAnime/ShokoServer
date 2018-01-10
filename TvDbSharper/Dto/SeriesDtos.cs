namespace TvDbSharper.Dto
{
    using System;

    public class Actor
    {
        public int Id { get; set; }

        public string Image { get; set; }

        public string ImageAdded { get; set; }

        public int? ImageAuthor { get; set; }

        public string LastUpdated { get; set; }

        public string Name { get; set; }

        public string Role { get; set; }

        public int? SeriesId { get; set; }

        public int? SortOrder { get; set; }
    }

    public class BasicEpisode
    {
        public int? AbsoluteNumber { get; set; }

        public int? AiredEpisodeNumber { get; set; }

        public int? AiredSeason { get; set; }

        public decimal? DvdEpisodeNumber { get; set; }

        public int? DvdSeason { get; set; }

        public string EpisodeName { get; set; }

        public string FirstAired { get; set; }

        public int Id { get; set; }

        public long LastUpdated { get; set; }

        public string Overview { get; set; }
    }

    public class EpisodeQuery
    {
        public int? AbsoluteNumber { get; set; }

        public int? AiredEpisode { get; set; }

        public int? AiredSeason { get; set; }

        public int? DvdEpisode { get; set; }

        public int? DvdSeason { get; set; }

        public string FirstAired { get; set; }

        public string ImdbId { get; set; }
    }

    public class EpisodesSummary
    {
        public string AiredEpisodes { get; set; }

        public string[] AiredSeasons { get; set; }

        public string DvdEpisodes { get; set; }

        public string[] DvdSeasons { get; set; }
    }

    public class Image
    {
        public string FileName { get; set; }

        public int? Id { get; set; }

        public string KeyType { get; set; }

        public int? LanguageId { get; set; }

        public RatingsInfo RatingsInfo { get; set; }

        public string Resolution { get; set; }

        public string SubKey { get; set; }

        public string Thumbnail { get; set; }
    }

    public class ImagesQuery
    {
        public KeyType KeyType { get; set; }

        public string Resolution { get; set; }

        public string SubKey { get; set; }
    }

    public class ImagesSummary
    {
        public int? Fanart { get; set; }

        public int? Poster { get; set; }

        public int? Season { get; set; }

        public int? Seasonwide { get; set; }

        public int? Series { get; set; }
    }

    public enum KeyType
    {
        // ReSharper disable once IdentifierTypo
        Fanart,

        Poster,

        Season,

        // ReSharper disable once IdentifierTypo
        Seasonwide,

        Series
    }

    public class RatingsInfo
    {
        public decimal? Average { get; set; }

        public int? Count { get; set; }
    }

    public class Series
    {
        public string Added { get; set; }

        public string AirsDayOfWeek { get; set; }

        public string AirsTime { get; set; }

        public string[] Aliases { get; set; }

        public string Banner { get; set; }

        public string FirstAired { get; set; }

        public string[] Genre { get; set; }

        public int Id { get; set; }

        public string ImdbId { get; set; }

        public long LastUpdated { get; set; }

        public string Network { get; set; }

        public string NetworkId { get; set; }

        public string Overview { get; set; }

        public string Rating { get; set; }

        public string Runtime { get; set; }

        /// <summary>
        /// <para>TV.com ID</para>
        /// <para>Don't confuse with the Id property.</para>
        /// <para>Usually it is an integer, but there is nothing stopping users of http://thetvdb.com from changing it into any value. 
        /// This has happened before.</para>
        /// </summary>
        public string SeriesId { get; set; }

        public string SeriesName { get; set; }

        public decimal? SiteRating { get; set; }

        public int? SiteRatingCount { get; set; }

        public string Status { get; set; }

        // ReSharper disable once InconsistentNaming
        public string Zap2itId { get; set; }
    }

    [Flags]
    public enum SeriesFilter
    {
        Banner = 1,

        Status = 2,

        Genre = 4,

        Rating = 8,

        NetworkId = 16,

        Overview = 32,

        ImdbId = 64,

        // ReSharper disable once InconsistentNaming
        Zap2itId = 128,

        Added = 256,

        AirsDayOfWeek = 512,

        AirsTime = 1024,

        SiteRating = 2048,

        Aliases = 4096,

        SeriesId = 8192,

        FirstAired = 16384,

        Runtime = 32768,

        LastUpdated = 65536,

        SiteRatingCount = 131072,

        Id = 262144,

        SeriesName = 524288,

        Network = 1048576,

        AddedBy = 2097152
    }
}