using System;
using System.Diagnostics;
using Newtonsoft.Json;

namespace Shoko.Commons.Plex.Collection
{
    [DebuggerDisplay("Key = {Key}, Title = {Title}")]
    public class PlexLibrary
    {
        [JsonProperty("ratingKey")] public string RatingKey { get; set; }
        [JsonProperty("key")] public string Key { get; set; }
        [JsonProperty("type")] public PlexType Type { get; set; }
        [JsonProperty("title")] public string Title { get; set; }
        [JsonProperty("contentRating")] public string ContentRating { get; set; }
        [JsonProperty("summary")] public string Summary { get; set; }
        [JsonProperty("index")] public long Index { get; set; }
        [JsonProperty("rating")] public double? Rating { get; set; }
        [JsonProperty("year")] public long? Year { get; set; }
        [JsonProperty("thumb")] public string Thumb { get; set; }
        [JsonProperty("art")] public string Art { get; set; }
        [JsonProperty("banner")] public string Banner { get; set; }
        [JsonProperty("originallyAvailableAt")] public DateTime? OriginallyAvailableAt { get; set; }
        [JsonProperty("leafCount")] public long LeafCount { get; set; }
        [JsonProperty("viewedLeafCount")] public long ViewedLeafCount { get; set; }
        [JsonProperty("childCount")] public long ChildCount { get; set; }
        [JsonProperty("addedAt")] public long AddedAt { get; set; }
        [JsonProperty("updatedAt")] public long UpdatedAt { get; set; }
        [JsonProperty("Genre")] public TagHolder[] Genre { get; set; }
        [JsonProperty("Role")] public TagHolder[] Role { get; set; }
        [JsonProperty("skipChildren")] public bool? SkipChildren { get; set; }
        [JsonProperty("theme")] public string Theme { get; set; }
        [JsonProperty("viewCount")] public long? ViewCount { get; set; }
        [JsonProperty("lastViewedAt")] public long? LastViewedAt { get; set; }
        [JsonProperty("titleSort")] public string TitleSort { get; set; }
        [JsonProperty("userRating")] public long? UserRating { get; set; }
    }
}