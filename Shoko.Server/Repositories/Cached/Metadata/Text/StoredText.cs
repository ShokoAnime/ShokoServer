using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.Repositories.Cached.Metadata.Text;

/// <summary>
///   A stored overview as the text cache hands it out: a detached copy of its
///   row, with the entry it belongs to.
/// </summary>
internal class StoredText : IText
{
    /// <inheritdoc />
    public required MetadataSource Source { get; init; }

    /// <inheritdoc />
    public required TitleLanguage Language { get; init; }

    /// <inheritdoc />
    public required string LanguageCode { get; init; }

    /// <inheritdoc />
    public string? CountryCode { get; init; }

    /// <inheritdoc />
    public string? ScriptCode { get; init; }

    /// <inheritdoc />
    public required string Value { get; init; }

    /// <summary>
    ///   The stored text's ID, unique among texts of its kind.
    /// </summary>
    public required int ID { get; init; }

    /// <summary>
    ///   The entry the stored text belongs to.
    /// </summary>
    public required MetadataGuid EntityID { get; init; }

    /// <summary>
    ///   The stored text a user's pick copies, when it is one.
    /// </summary>
    public int? ReferenceID { get; init; }

    /// <inheritdoc />
    public bool IsEnabled { get; init; } = true;

    /// <inheritdoc />
    public TextPreference Preference { get; init; }

    /// <inheritdoc />
    public int Ordering { get; init; }

    int? IText.ID => ID;

    MetadataGuid? IText.EntityID => EntityID;

    // Stored among the entry's texts, so never the entry's inline default.
    bool IText.IsInlineDefault => false;

    /// <summary>
    ///   Whether the text is a title or an overview.
    /// </summary>
    public virtual TextKind Kind => TextKind.Overview;

    /// <inheritdoc />
    public bool Equals(IText? other)
        => IText.Equals(this, other);
}

/// <summary>
///   A stored title as the text cache hands it out.
/// </summary>
internal sealed class StoredTitle : StoredText, ITitle
{
    /// <inheritdoc />
    public required TitleType Type { get; init; }

    /// <inheritdoc />
    public override TextKind Kind => TextKind.Title;

    // A stored title was never synthesized on the spot.
    bool ITitle.IsSynthesized => false;

    /// <inheritdoc />
    public bool Equals(ITitle? other)
        => ITitle.Equals(this, other);
}
