using System;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace Shoko.Models.Plex.TVShow
{
    [DebuggerDisplay("Key = {Key}, Title = {Title}")]
    public class Episode
    {
        [DataMember(Name = "ratingKey")] public string RatingKey { get; set; }
        [DataMember(Name = "key")] public string Key { get; set; }
        [DataMember(Name = "parentRatingKey")] public string ParentRatingKey { get; set; }
        [DataMember(Name = "studio")] public string Studio { get; set; }
        [DataMember(Name = "type")] public PlexType Type { get; set; }
        [DataMember(Name = "title")] public string Title { get; set; }
        [DataMember(Name = "parentKey")] public string ParentKey { get; set; }
        [DataMember(Name = "grandparentTitle")] public string GrandparentTitle { get; set; }
        [DataMember(Name = "parentTitle")] public string ParentTitle { get; set; }
        [DataMember(Name = "contentRating")] public string ContentRating { get; set; }
        [DataMember(Name = "summary")] public string Summary { get; set; }
        [DataMember(Name = "index")] public long Index { get; set; }
        [DataMember(Name = "parentIndex")] public long ParentIndex { get; set; }
        [DataMember(Name = "rating")] public double? Rating { get; set; }
        [DataMember(Name = "viewCount")] public long? ViewCount { get; set; }
        [DataMember(Name = "lastViewedAt")] public long? LastViewedAt { get; set; }
        [DataMember(Name = "year")] public long? Year { get; set; }
        [DataMember(Name = "thumb")] public string Thumb { get; set; }
        [DataMember(Name = "art")] public string Art { get; set; }
        [DataMember(Name = "parentThumb")] public string ParentThumb { get; set; }
        [DataMember(Name = "grandparentThumb")] public string GrandparentThumb { get; set; }
        [DataMember(Name = "grandparentArt")] public string GrandparentArt { get; set; }
        [DataMember(Name = "grandparentTheme")] public string GrandparentTheme { get; set; }
        [DataMember(Name = "duration")] public long Duration { get; set; }
        [DataMember(Name = "originallyAvailableAt")] public DateTime? OriginallyAvailableAt { get; set; }
        [DataMember(Name = "addedAt")] public long AddedAt { get; set; }
        [DataMember(Name = "updatedAt")] public long UpdatedAt { get; set; }
        [DataMember(Name = "chapterSource")] public string ChapterSource { get; set; }
        [DataMember(Name = "Media")] public Media[] Media { get; set; }
        [DataMember(Name = "Director")] public TagHolder[] Director { get; set; }
        [DataMember(Name = "Writer")] public TagHolder[] Writer { get; set; }
        [DataMember(Name = "titleSort")] public string TitleSort { get; set; }
    }
}