using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Abstractions.Metadata.Text;

/// <summary>
///   A partial update of a stored text through
///   <see cref="IMetadataTextManager.UpdateText"/>. Every property left
///   <c>null</c> keeps its current value.
/// </summary>
public sealed class TextUpdateData
{
    /// <summary>
    ///   Whether the text may be listed and chosen.
    /// </summary>
    public bool? IsEnabled { get; init; }

    /// <summary>
    ///   How strongly the text is preferred. Setting it clears the same
    ///   preference on the entry's other texts of the same kind, and for
    ///   <see cref="TextPreference.Language"/> only on those in the same
    ///   language.
    /// </summary>
    public TextPreference? Preference { get; init; }

    /// <summary>
    ///   The new value. Only a text from <see cref="MetadataSource.User"/>
    ///   that picks no other text can be given a new value, since a source's
    ///   own text is written back on its next refresh.
    /// </summary>
    public string? Value { get; init; }

    /// <summary>
    ///   The new position among the texts of the same kind its source gave the
    ///   entry.
    /// </summary>
    public int? Ordering { get; init; }
}
