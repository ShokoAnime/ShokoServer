using System;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace Shoko.Models.Plex.Collection
{
    [DebuggerDisplay("Key = {Key}, Title = {Title}")]
    public class PlexLibrary
    {
        [DataMember(Name = "ratingKey")] public string RatingKey { get; set; }
        [DataMember(Name = "key")] public string Key { get; set; }
        [DataMember(Name = "type")] public PlexType Type { get; set; }
        [DataMember(Name = "title")] public string Title { get; set; }
        [DataMember(Name = "contentRating")] public string ContentRating { get; set; }
        [DataMember(Name = "summary")] public string Summary { get; set; }
        [DataMember(Name = "index")] public long Index { get; set; }
        [DataMember(Name = "rating")] public double? Rating { get; set; }
        [DataMember(Name = "year")] public long? Year { get; set; }
        [DataMember(Name = "thumb")] public string Thumb { get; set; }
        [DataMember(Name = "art")] public string Art { get; set; }
        [DataMember(Name = "banner")] public string Banner { get; set; }
        [DataMember(Name = "originallyAvailableAt")] public DateTime? OriginallyAvailableAt { get; set; }
        [DataMember(Name = "leafCount")] public long LeafCount { get; set; }
        [DataMember(Name = "viewedLeafCount")] public long ViewedLeafCount { get; set; }
        [DataMember(Name = "childCount")] public long ChildCount { get; set; }
        [DataMember(Name = "addedAt")] public long AddedAt { get; set; }
        [DataMember(Name = "updatedAt")] public long UpdatedAt { get; set; }
        [DataMember(Name = "Genre")] public TagHolder[] Genre { get; set; }
        [DataMember(Name = "Role")] public TagHolder[] Role { get; set; }
        [DataMember(Name = "skipChildren")] public bool? SkipChildren { get; set; }
        [DataMember(Name = "theme")] public string Theme { get; set; }
        [DataMember(Name = "viewCount")] public long? ViewCount { get; set; }
        [DataMember(Name = "lastViewedAt")] public long? LastViewedAt { get; set; }
        [DataMember(Name = "titleSort")] public string TitleSort { get; set; }
        [DataMember(Name = "userRating")] public long? UserRating { get; set; }
    }
}