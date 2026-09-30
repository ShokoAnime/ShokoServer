using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.Models.Metadata;

/// <summary>
///   The columns every stored title and overview has: the entry it belongs
///   to, who wrote it, its language, its value and what a user set on it.
/// </summary>
/// <remarks>
///   Only written and migrated through these rows; the text cache reads the
///   tables with plain queries and keeps its own compact copy.
/// </remarks>
public abstract class MetadataTextRow
{
    #region Database Columns

    /// <summary>
    ///   The source of the entry the text belongs to.
    /// </summary>
    public MetadataSource EntitySource { get; set; } = null!;

    /// <summary>
    ///   What kind of entry the text belongs to.
    /// </summary>
    public MetadataEntityType EntityType { get; set; } = null!;

    /// <summary>
    ///   The entry's own ID on <see cref="EntitySource"/>.
    /// </summary>
    public string EntityID { get; set; } = string.Empty;

    /// <summary>
    ///   The source the text is from, which differs from
    ///   <see cref="EntitySource"/> for text added to another source's entry.
    /// </summary>
    public MetadataSource Source { get; set; } = null!;

    /// <summary>
    ///   The language, as worked out from the codes.
    /// </summary>
    public TitleLanguage Language { get; set; }

    /// <summary>
    ///   The language code, or <c>unk</c> when it is not known.
    /// </summary>
    public string LanguageCode { get; set; } = "unk";

    /// <summary>
    ///   The country code, when there is one.
    /// </summary>
    public string? CountryCode { get; set; }

    /// <summary>
    ///   The ISO 15924 script code, when it is known.
    /// </summary>
    public string? ScriptCode { get; set; }

    /// <summary>
    ///   The text itself.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    ///   Whether the text may be listed and chosen.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    ///   How strongly a user prefers the text for its entry.
    /// </summary>
    public TextPreference Preference { get; set; }

    /// <summary>
    ///   Where the text sits among the entry's texts of the same source and
    ///   kind, from <c>0</c>.
    /// </summary>
    public int Ordering { get; set; }

    /// <summary>
    ///   The stored text of the same kind a user's pick copies, when it is
    ///   one.
    /// </summary>
    public int? ReferenceID { get; set; }

    #endregion

    #region Helpers

    /// <summary>
    ///   The row's ID, which is <c>0</c> until it has been saved.
    /// </summary>
    public abstract int RowID { get; set; }

    /// <summary>
    ///   Whether the row is a title or an overview.
    /// </summary>
    public abstract TextKind Kind { get; }

    /// <summary>
    ///   The kind of title, or <see cref="TitleType.None"/> for an overview.
    /// </summary>
    public virtual TitleType TitleType
    {
        get => TitleType.None;
        set { }
    }

    /// <summary>
    ///   The entry the text belongs to.
    /// </summary>
    public MetadataGuid EntryID
    {
        get => new(EntitySource, EntityType, EntityID);
        set
        {
            EntitySource = value.Source;
            EntityType = value.EntityType;
            EntityID = value.ID;
        }
    }

    /// <summary>
    ///   A new row of the same kind.
    /// </summary>
    /// <param name="kind">Titles or overviews.</param>
    /// <returns>The row, with an ID of <c>0</c>.</returns>
    public static MetadataTextRow Create(TextKind kind)
        => kind is TextKind.Title ? new Metadata_Title() : new Metadata_Overview();

    #endregion
}
