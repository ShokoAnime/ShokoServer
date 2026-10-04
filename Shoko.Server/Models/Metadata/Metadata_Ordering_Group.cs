using Shoko.Abstractions.Metadata;

namespace Shoko.Server.Models.Metadata;

/// <summary>
///   One group of a stored ordering, read back as a season of the ordering.
///   Its titles and overviews are in the text store, under its <see cref="ID"/>.
/// </summary>
public class Metadata_Ordering_Group : IMetadataStoreRow<Metadata_Ordering_Group>
{
    #region Database Columns

    /// <summary>
    ///   The row's ID.
    /// </summary>
    public int Metadata_Ordering_GroupID { get; set; }

    /// <summary>
    ///   The source the ordering is stored under.
    /// </summary>
    public MetadataSource Source { get; set; } = null!;

    /// <summary>
    ///   The source's own ID for the group, which names it as a season.
    /// </summary>
    public string ProviderID { get; set; } = string.Empty;

    /// <summary>
    ///   The source's own ID for the ordering the group is in.
    /// </summary>
    public string OrderingID { get; set; } = string.Empty;

    /// <summary>
    ///   Where the group sits in the ordering, from <c>0</c>.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    ///   Whether the group holds the ordering's specials, read back as
    ///   season <c>0</c>.
    /// </summary>
    public bool IsSpecial { get; set; }

    /// <summary>
    ///   The season number the source gave the group, or <c>null</c> to
    ///   number it by its place among the ordering's regular groups.
    /// </summary>
    public int? SeasonNumber { get; set; }

    #endregion

    #region Helpers

    /// <summary>
    ///   The group's identifier, as a season.
    /// </summary>
    public MetadataGuid ID => new(Source, MetadataEntityType.Season, ProviderID);

    /// <summary>
    ///   The identifier of the ordering the group is in.
    /// </summary>
    public MetadataGuid OrderingGuid => new(Source, MetadataEntityType.Ordering, OrderingID);

    /// <summary>
    ///   Whether the stored columns are the same as another row's, leaving
    ///   out the row's ID.
    /// </summary>
    /// <param name="other">The other row.</param>
    /// <returns><c>true</c> when nothing but the ID differs.</returns>
    internal bool SameAs(Metadata_Ordering_Group other)
        => Source == other.Source &&
            ProviderID == other.ProviderID &&
            OrderingID == other.OrderingID &&
            Position == other.Position &&
            IsSpecial == other.IsSpecial &&
            SeasonNumber == other.SeasonNumber;

    #endregion

    #region Store Row Implementation

    int IMetadataStoreRow<Metadata_Ordering_Group>.RowID
    {
        get => Metadata_Ordering_GroupID;
        set => Metadata_Ordering_GroupID = value;
    }

    Metadata_Ordering_Group IMetadataStoreRow<Metadata_Ordering_Group>.Clone()
        => (Metadata_Ordering_Group)MemberwiseClone();

    #endregion
}
