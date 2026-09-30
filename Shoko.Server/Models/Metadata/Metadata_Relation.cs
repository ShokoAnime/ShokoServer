using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.Models.Metadata;

/// <summary>
///   An authored relation from one entry to another on the same source, as
///   the relation store keeps it. Read through the store, which also reads it
///   back reversed from the other end.
/// </summary>
public class Metadata_Relation : IMetadataStoreRow<Metadata_Relation>
{
    #region Database Columns

    /// <summary>
    ///   The row's ID.
    /// </summary>
    public int Metadata_RelationID { get; set; }

    /// <summary>
    ///   The source both entries belong to.
    /// </summary>
    public MetadataSource Source { get; set; } = null!;

    /// <summary>
    ///   What kind of entry the relation is stated from.
    /// </summary>
    public MetadataEntityType BaseType { get; set; } = null!;

    /// <summary>
    ///   The source's ID for the entry the relation is stated from.
    /// </summary>
    public string BaseID { get; set; } = string.Empty;

    /// <summary>
    ///   What kind of entry the related one is.
    /// </summary>
    public MetadataEntityType RelatedType { get; set; } = null!;

    /// <summary>
    ///   The source's ID for the related entry.
    /// </summary>
    public string RelatedID { get; set; } = string.Empty;

    /// <summary>
    ///   What the related entry is to the base one.
    /// </summary>
    public RelationType RelationType { get; set; }

    /// <summary>
    ///   Where the relation sits among the base entry's relations, from
    ///   <c>0</c>.
    /// </summary>
    public int Ordering { get; set; }

    #endregion

    #region Store Row Implementation

    int IMetadataStoreRow<Metadata_Relation>.RowID
    {
        get => Metadata_RelationID;
        set => Metadata_RelationID = value;
    }

    Metadata_Relation IMetadataStoreRow<Metadata_Relation>.Clone()
        => (Metadata_Relation)MemberwiseClone();

    #endregion
}
