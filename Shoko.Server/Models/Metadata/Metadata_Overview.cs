using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.Models.Metadata;

/// <summary>
///   One stored overview of an entry, from any source.
/// </summary>
public class Metadata_Overview : MetadataTextRow
{
    /// <summary>
    ///   The row's ID.
    /// </summary>
    public int Metadata_OverviewID { get; set; }

    /// <inheritdoc />
    public override int RowID
    {
        get => Metadata_OverviewID;
        set => Metadata_OverviewID = value;
    }

    /// <inheritdoc />
    public override TextKind Kind => TextKind.Overview;
}
