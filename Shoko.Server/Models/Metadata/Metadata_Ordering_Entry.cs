using Shoko.Abstractions.Metadata;

namespace Shoko.Server.Models.Metadata;

/// <summary>
///   One episode's place in a group of a stored ordering.
/// </summary>
public class Metadata_Ordering_Entry : IMetadataStoreRow<Metadata_Ordering_Entry>
{
    #region Database Columns

    /// <summary>
    ///   The row's ID.
    /// </summary>
    public int Metadata_Ordering_EntryID { get; set; }

    /// <summary>
    ///   The source the ordering is stored under.
    /// </summary>
    public MetadataSource Source { get; set; } = null!;

    /// <summary>
    ///   The source's own ID for the ordering.
    /// </summary>
    public string OrderingID { get; set; } = string.Empty;

    /// <summary>
    ///   The source's own ID for the group the episode is in.
    /// </summary>
    public string GroupID { get; set; } = string.Empty;

    /// <summary>
    ///   Where the episode sits in the group, from <c>0</c>.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    ///   The episode's source, which is the ordered series' source.
    /// </summary>
    public MetadataSource EpisodeSource { get; set; } = null!;

    /// <summary>
    ///   The episode's own ID on its source.
    /// </summary>
    public string EpisodeID { get; set; } = string.Empty;

    #endregion

    #region Helpers

    /// <summary>
    ///   The identifier of the ordering the place is in.
    /// </summary>
    public MetadataGuid OrderingGuid => new(Source, MetadataEntityType.Ordering, OrderingID);

    /// <summary>
    ///   The identifier of the group the place is in, as a season.
    /// </summary>
    public MetadataGuid GroupGuid => new(Source, MetadataEntityType.Season, GroupID);

    /// <summary>
    ///   The episode's identifier.
    /// </summary>
    public MetadataGuid EpisodeGuid => new(EpisodeSource, MetadataEntityType.Episode, EpisodeID);

    /// <summary>
    ///   Whether the stored columns are the same as another row's, leaving
    ///   out the row's ID.
    /// </summary>
    /// <param name="other">The other row.</param>
    /// <returns><c>true</c> when nothing but the ID differs.</returns>
    internal bool SameAs(Metadata_Ordering_Entry other)
        => Source == other.Source &&
            OrderingID == other.OrderingID &&
            GroupID == other.GroupID &&
            Position == other.Position &&
            EpisodeSource == other.EpisodeSource &&
            EpisodeID == other.EpisodeID;

    #endregion

    #region Store Row Implementation

    int IMetadataStoreRow<Metadata_Ordering_Entry>.RowID
    {
        get => Metadata_Ordering_EntryID;
        set => Metadata_Ordering_EntryID = value;
    }

    Metadata_Ordering_Entry IMetadataStoreRow<Metadata_Ordering_Entry>.Clone()
        => (Metadata_Ordering_Entry)MemberwiseClone();

    #endregion
}
