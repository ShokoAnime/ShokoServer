using System;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;

namespace Shoko.Commons.Plex.TVShow
{
    [DebuggerDisplay("Key = {Key}, Title = {Title}")]
    public class Episode
    {
        [JsonProperty("ratingKey")] public string RatingKey { get; set; }
        [JsonProperty("key")] public string Key { get; set; }
        [JsonProperty("parentRatingKey")] public string ParentRatingKey { get; set; }
        [JsonProperty("studio")] public string Studio { get; set; }
        [JsonProperty("type")] public PlexType Type { get; set; }
        [JsonProperty("title")] public string Title { get; set; }
        [JsonProperty("parentKey")] public string ParentKey { get; set; }
        [JsonProperty("grandparentTitle")] public string GrandparentTitle { get; set; }
        [JsonProperty("parentTitle")] public string ParentTitle { get; set; }
        [JsonProperty("contentRating")] public string ContentRating { get; set; }
        [JsonProperty("summary")] public string Summary { get; set; }
        [JsonProperty("index")] public long Index { get; set; }
        [JsonProperty("parentIndex")] public long ParentIndex { get; set; }
        [JsonProperty("rating")] public double? Rating { get; set; }
        [JsonProperty("viewCount")] public long? ViewCount { get; set; }
        [JsonProperty("lastViewedAt")] public long? LastViewedAt { get; set; }
        [JsonProperty("year")] public long? Year { get; set; }
        [JsonProperty("thumb")] public string Thumb { get; set; }
        [JsonProperty("art")] public string Art { get; set; }
        [JsonProperty("parentThumb")] public string ParentThumb { get; set; }
        [JsonProperty("grandparentThumb")] public string GrandparentThumb { get; set; }
        [JsonProperty("grandparentArt")] public string GrandparentArt { get; set; }
        [JsonProperty("grandparentTheme")] public string GrandparentTheme { get; set; }
        [JsonProperty("duration")] public long Duration { get; set; }
        [JsonProperty("originallyAvailableAt")] public DateTime? OriginallyAvailableAt { get; set; }
        [JsonProperty("addedAt")] public long AddedAt { get; set; }
        [JsonProperty("updatedAt")] public long UpdatedAt { get; set; }
        [JsonProperty("chapterSource")] public string ChapterSource { get; set; }
        [JsonProperty("Media")] public Media[] Media { get; set; }
        [JsonProperty("Director")] public TagHolder[] Director { get; set; }
        [JsonProperty("Writer")] public TagHolder[] Writer { get; set; }
        [JsonProperty("titleSort")] public string TitleSort { get; set; }
    }
}