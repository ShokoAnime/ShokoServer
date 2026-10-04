using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.API.v3.Models.TextManagement;

/// <summary>
///   A title or an overview as the text management routes show it: a stored
///   text with its ID and a user's flags, or a source's default kept on its
///   entry's own row, which has no ID and can only be overridden.
/// </summary>
public class ManagedText
{
    /// <summary>
    ///   The stored text's ID, unique among texts of its kind. <c>null</c> for
    ///   a default kept on the entry's row, or a synthesized episode title.
    /// </summary>
    public int? ID { get; set; }

    /// <summary>
    ///   Whether the text is a title or an overview. IDs are per kind, so a
    ///   text is named by both.
    /// </summary>
    [Required]
    public TextKind Kind { get; set; }

    /// <summary>
    ///   The source of the entry the text is stored on. <c>null</c> when the
    ///   text is not stored.
    /// </summary>
    public MetadataSource? EntitySource { get; set; }

    /// <summary>
    ///   The kind of the entry the text is stored on. <c>null</c> when the
    ///   text is not stored.
    /// </summary>
    public MetadataEntityType? EntityType { get; set; }

    /// <summary>
    ///   The source's own ID of the entry the text is stored on. <c>null</c>
    ///   when the text is not stored.
    /// </summary>
    public string? EntityID { get; set; }

    /// <summary>
    ///   Who wrote the text.
    /// </summary>
    [Required]
    public MetadataSource Source { get; set; }

    /// <summary>
    ///   The text itself.
    /// </summary>
    [Required]
    public string Value { get; set; }

    /// <summary>
    ///   The language, as a code such as <c>en</c>, <c>x-jat</c> or
    ///   <c>zh-Hans</c>.
    /// </summary>
    [Required]
    public string Language { get; set; }

    /// <summary>
    ///   The language code exactly as the source gave it.
    /// </summary>
    [Required]
    public string LanguageCode { get; set; }

    /// <summary>
    ///   The country code as the source gave it, if any.
    /// </summary>
    public string? CountryCode { get; set; }

    /// <summary>
    ///   The ISO 15924 script code, if the source gave one.
    /// </summary>
    public string? ScriptCode { get; set; }

    /// <summary>
    ///   The kind of title. Left out for an overview.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public TitleType? TitleType { get; set; }

    /// <summary>
    ///   Whether the text may be listed and chosen.
    /// </summary>
    [Required]
    public bool IsEnabled { get; set; }

    /// <summary>
    ///   How strongly a user prefers the text for its entry.
    /// </summary>
    [Required]
    public TextPreference Preference { get; set; }

    /// <summary>
    ///   Where the text sits among the texts of the same kind its source gave
    ///   the entry.
    /// </summary>
    [Required]
    public int Ordering { get; set; }

    /// <summary>
    ///   The ID of the stored text, of the same kind, a user's pick copies and
    ///   follows. <c>null</c> when the text picks no other text.
    /// </summary>
    public int? ReferenceID { get; set; }

    /// <summary>
    ///   Whether the text is the source's default kept on the entry's own
    ///   row. Such a text can be overridden by a preference, but not changed,
    ///   disabled or removed.
    /// </summary>
    [Required]
    public bool Inline { get; set; }

    /// <summary>
    ///   Set when the title was synthesized for an episode no source named. Left
    ///   out otherwise.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public bool? Synthesized { get; set; }

    /// <summary>
    ///   Whether the text is the entry's default. Only set when listing an
    ///   entry's texts.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public bool? Default { get; set; }

    /// <summary>
    ///   Whether the text is the one chosen for the entry. Only set when
    ///   listing an entry's texts.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public bool? Preferred { get; set; }

    /// <summary>
    ///   Builds the model of a text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="isDefault">Optional. Whether it is the entry's default, when listing an entry's texts.</param>
    /// <param name="isPreferred">Optional. Whether it is the entry's chosen text, when listing an entry's texts.</param>
    public ManagedText(IText text, bool? isDefault = null, bool? isPreferred = null)
    {
        ID = text.ID;
        Kind = text is ITitle ? TextKind.Title : TextKind.Overview;
        EntitySource = text.EntityID?.Source;
        EntityType = text.EntityID?.EntityType;
        EntityID = text.EntityID?.ID;
        Source = text.Source;
        Value = text.Value;
        Language = text.Language.GetString();
        LanguageCode = text.LanguageCode;
        CountryCode = text.CountryCode;
        ScriptCode = text.ScriptCode;
        TitleType = text is ITitle title ? title.Type : null;
        IsEnabled = text.IsEnabled;
        Preference = text.Preference;
        Ordering = text.Ordering;
        ReferenceID = text.ReferenceID;
        Inline = text.IsInlineDefault;
        Synthesized = text is ITitle { IsSynthesized: true } ? true : null;
        Default = isDefault;
        Preferred = isPreferred;
    }
}
