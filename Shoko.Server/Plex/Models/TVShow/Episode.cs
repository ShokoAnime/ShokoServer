using System;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace Shoko.Server.Plex.Models.TVShow;

[DebuggerDisplay("Key = {Key}, Title = {Title}")]
public class Episode
{
    [DataMember(Name = "ratingKey")] public string RatingKey { get; set; } = null!;
    [DataMember(Name = "key")] public string Key { get; set; } = null!;
    [DataMember(Name = "parentRatingKey")] public string ParentRatingKey { get; set; } = null!;
    [DataMember(Name = "studio")] public string Studio { get; set; } = null!;
    [DataMember(Name = "type")] public PlexType Type { get; set; }
    [DataMember(Name = "title")] public string Title { get; set; } = null!;
    [DataMember(Name = "parentKey")] public string ParentKey { get; set; } = null!;
    [DataMember(Name = "grandparentTitle")] public string GrandparentTitle { get; set; } = null!;
    [DataMember(Name = "parentTitle")] public string ParentTitle { get; set; } = null!;
    [DataMember(Name = "contentRating")] public string ContentRating { get; set; } = null!;
    [DataMember(Name = "summary")] public string Summary { get; set; } = null!;
    [DataMember(Name = "index")] public long Index { get; set; }
    [DataMember(Name = "parentIndex")] public long ParentIndex { get; set; }
    [DataMember(Name = "rating")] public double? Rating { get; set; }
    [DataMember(Name = "viewCount")] public long? ViewCount { get; set; }
    [DataMember(Name = "lastViewedAt")] public long? LastViewedAt { get; set; }
    [DataMember(Name = "year")] public long? Year { get; set; }
    [DataMember(Name = "thumb")] public string Thumb { get; set; } = null!;
    [DataMember(Name = "art")] public string Art { get; set; } = null!;
    [DataMember(Name = "parentThumb")] public string ParentThumb { get; set; } = null!;
    [DataMember(Name = "grandparentThumb")] public string GrandparentThumb { get; set; } = null!;
    [DataMember(Name = "grandparentArt")] public string GrandparentArt { get; set; } = null!;
    [DataMember(Name = "grandparentTheme")] public string GrandparentTheme { get; set; } = null!;
    [DataMember(Name = "duration")] public long Duration { get; set; }
    [DataMember(Name = "originallyAvailableAt")] public DateTime? OriginallyAvailableAt { get; set; }
    [DataMember(Name = "addedAt")] public long AddedAt { get; set; }
    [DataMember(Name = "updatedAt")] public long UpdatedAt { get; set; }
    [DataMember(Name = "chapterSource")] public string ChapterSource { get; set; } = null!;
    [DataMember(Name = "Media")] public Media[] Media { get; set; } = null!;
    [DataMember(Name = "Director")] public TagHolder[] Director { get; set; } = null!;
    [DataMember(Name = "Writer")] public TagHolder[] Writer { get; set; } = null!;
    [DataMember(Name = "titleSort")] public string TitleSort { get; set; } = null!;
}
