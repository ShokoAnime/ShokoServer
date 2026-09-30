using Shoko.Abstractions.Metadata;

namespace Shoko.Server.Models.Metadata;

/// <summary>
///   One series or movie a stored collection gathers, on the collection's
///   own source.
/// </summary>
public class Metadata_Collection_Member : IMetadataStoreRow<Metadata_Collection_Member>
{
    #region Database Columns

    /// <summary>
    ///   The row's ID.
    /// </summary>
    public int Metadata_Collection_MemberID { get; set; }

    /// <summary>
    ///   The source the collection and its member belong to.
    /// </summary>
    public MetadataSource Source { get; set; } = null!;

    /// <summary>
    ///   The source's own ID for the collection.
    /// </summary>
    public string CollectionID { get; set; } = string.Empty;

    /// <summary>
    ///   Whether the member is a series or a movie.
    /// </summary>
    public MetadataEntityType MemberType { get; set; } = null!;

    /// <summary>
    ///   The source's own ID for the member.
    /// </summary>
    public string MemberID { get; set; } = string.Empty;

    /// <summary>
    ///   Where the member sits in the collection, from <c>0</c>.
    /// </summary>
    public int Ordering { get; set; }

    #endregion

    #region Helpers

    /// <summary>
    ///   The member's identifier.
    /// </summary>
    public MetadataGuid MemberGuid => new(Source, MemberType, MemberID);

    #endregion

    #region Store Row Implementation

    int IMetadataStoreRow<Metadata_Collection_Member>.RowID
    {
        get => Metadata_Collection_MemberID;
        set => Metadata_Collection_MemberID = value;
    }

    Metadata_Collection_Member IMetadataStoreRow<Metadata_Collection_Member>.Clone()
        => (Metadata_Collection_Member)MemberwiseClone();

    #endregion
}
