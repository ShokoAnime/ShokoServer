using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Abstractions.Metadata.Text.Options;

/// <summary>
///   Options for filtering the texts <see cref="IMetadataTextManager"/> lists.
/// </summary>
public sealed class TextFilteringOptions
{
    /// <summary>
    ///   Optional. Only texts from this source.
    /// </summary>
    public MetadataSource? Source { get; set; }

    /// <summary>
    ///   Optional. Only texts in this language.
    /// </summary>
    public TitleLanguage? Language { get; set; }

    /// <summary>
    ///   Optional. Only titles of this type. Leaves out every overview.
    /// </summary>
    public TitleType? TitleType { get; set; }

    /// <summary>
    ///   Optional. Only texts with this preference.
    /// </summary>
    public TextPreference? Preference { get; set; }

    /// <summary>
    ///   Optional. <c>true</c> for enabled texts only, <c>false</c> for
    ///   disabled ones only, or <c>null</c> for both. Defaults to <c>true</c>.
    /// </summary>
    public bool? IsEnabled { get; set; } = true;

    /// <summary>
    ///   Whether to list the defaults kept on rows rather than stored: the
    ///   entry's own, ahead of the stored texts where the entry does not list
    ///   it elsewhere, and for a Shoko entry those of the entries it is
    ///   linked to. Defaults to <c>true</c>.
    /// </summary>
    public bool IncludeInlineDefault { get; set; } = true;

    /// <summary>
    ///   Optional. When listing texts across entries, only those of entries
    ///   from this source.
    /// </summary>
    public MetadataSource? EntitySource { get; set; }

    /// <summary>
    ///   Optional. When listing texts across entries, only those of entries of
    ///   this kind.
    /// </summary>
    public MetadataEntityType? EntityType { get; set; }
}
