using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.Repositories.Cached.Metadata.Text;

/// <summary>
///   One stored title or overview as the text cache holds it: 24 bytes, with
///   the language codes and the source kept once in the cache's own tables.
/// </summary>
/// <param name="id">The row's ID, unique within its kind.</param>
/// <param name="kind">Whether the row is a title or an overview.</param>
/// <param name="source">The index of the text's source in the cache's source table.</param>
/// <param name="locale">The index of the text's language and codes in the cache's locale table.</param>
/// <param name="titleType">The kind of title, or none for an overview.</param>
/// <param name="isEnabled">Whether the text may be listed and chosen.</param>
/// <param name="preference">How strongly a user prefers the text.</param>
/// <param name="ordering">The position among the entry's texts of the same source and kind.</param>
/// <param name="referenceID">The stored text a pick copies, or <c>0</c>.</param>
/// <param name="value">The title's value, pooled; <c>null</c> for an overview, whose value is loaded when asked for.</param>
internal readonly struct TextRow(
    int id,
    TextKind kind,
    byte source,
    ushort locale,
    TitleType titleType,
    bool isEnabled,
    TextPreference preference,
    int ordering,
    int referenceID,
    string? value
)
{
    private const int KindBit = 0x01;

    private const int EnabledBit = 0x02;

    private const int PreferenceShift = 2;

    private const int PreferenceMask = 0x03;

    private const int TitleTypeShift = 4;

    private const int TitleTypeMask = 0x0F;

    private readonly byte _bits = (byte)(
        (kind is TextKind.Overview ? KindBit : 0) |
        (isEnabled ? EnabledBit : 0) |
        (((int)preference & PreferenceMask) << PreferenceShift) |
        (((int)titleType & TitleTypeMask) << TitleTypeShift)
    );

    /// <summary>
    ///   The title's value, or <c>null</c> for an overview.
    /// </summary>
    public string? Value { get; } = value;

    /// <summary>
    ///   The row's ID, unique within its kind.
    /// </summary>
    public int ID { get; } = id;

    /// <summary>
    ///   The position among the entry's texts of the same source and kind.
    /// </summary>
    public int Ordering { get; } = ordering;

    /// <summary>
    ///   The stored text a pick copies, or <c>0</c> when the row is no pick.
    /// </summary>
    public int ReferenceID { get; } = referenceID;

    /// <summary>
    ///   The index of the text's language and codes in the locale table.
    /// </summary>
    public ushort Locale { get; } = locale;

    /// <summary>
    ///   The index of the text's source in the source table.
    /// </summary>
    public byte Source { get; } = source;

    /// <summary>
    ///   Whether the row is a title or an overview.
    /// </summary>
    public TextKind Kind => (_bits & KindBit) is 0 ? TextKind.Title : TextKind.Overview;

    /// <summary>
    ///   Whether the text may be listed and chosen.
    /// </summary>
    public bool IsEnabled => (_bits & EnabledBit) is not 0;

    /// <summary>
    ///   How strongly a user prefers the text.
    /// </summary>
    public TextPreference Preference => (TextPreference)((_bits >> PreferenceShift) & PreferenceMask);

    /// <summary>
    ///   The kind of title, or none for an overview.
    /// </summary>
    public TitleType TitleType => (TitleType)((_bits >> TitleTypeShift) & TitleTypeMask);
}
