using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.Models.Metadata;

/// <summary>
///   One stored title or other name of an entry, from any source.
/// </summary>
public class Metadata_Title : MetadataTextRow
{
    /// <summary>
    ///   The row's ID.
    /// </summary>
    public int Metadata_TitleID { get; set; }

    /// <summary>
    ///   The kind of title.
    /// </summary>
    public override TitleType TitleType { get; set; }

    /// <inheritdoc />
    public override int RowID
    {
        get => Metadata_TitleID;
        set => Metadata_TitleID = value;
    }

    /// <inheritdoc />
    public override TextKind Kind => TextKind.Title;
}
