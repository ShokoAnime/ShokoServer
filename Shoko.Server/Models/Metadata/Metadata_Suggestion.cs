using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.Models.Metadata;

/// <summary>
///   A suggestion from one entry to another on the same source, as the
///   suggestion store keeps it.
/// </summary>
public class Metadata_Suggestion : IMetadataStoreRow<Metadata_Suggestion>
{
    #region Database Columns

    /// <summary>
    ///   The row's ID.
    /// </summary>
    public int Metadata_SuggestionID { get; set; }

    /// <summary>
    ///   The source both entries belong to.
    /// </summary>
    public MetadataSource Source { get; set; } = null!;

    /// <summary>
    ///   What kind of entry makes the suggestion.
    /// </summary>
    public MetadataEntityType BaseType { get; set; } = null!;

    /// <summary>
    ///   The source's ID for the entry making the suggestion.
    /// </summary>
    public string BaseID { get; set; } = string.Empty;

    /// <summary>
    ///   What kind of entry is suggested.
    /// </summary>
    public MetadataEntityType SuggestedType { get; set; } = null!;

    /// <summary>
    ///   The source's ID for the suggested entry.
    /// </summary>
    public string SuggestedID { get; set; } = string.Empty;

    /// <summary>
    ///   Whether it is a recommendation or a likeness.
    /// </summary>
    public SuggestionKind Kind { get; set; }

    /// <summary>
    ///   The source's own ranking, best first from <c>0</c>, when it ranks.
    /// </summary>
    public int? Ranking { get; set; }

    /// <summary>
    ///   The percentage of voters who approve, when the source votes.
    /// </summary>
    public double? ApprovalRating { get; set; }

    /// <summary>
    ///   How many voted, when the source says.
    /// </summary>
    public int? Votes { get; set; }

    /// <summary>
    ///   The source's net score, when it keeps one. May be negative.
    /// </summary>
    public int? Score { get; set; }

    /// <summary>
    ///   Where the suggestion sits in the list it was given in, from <c>0</c>.
    /// </summary>
    public int Ordering { get; set; }

    #endregion

    #region Store Row Implementation

    int IMetadataStoreRow<Metadata_Suggestion>.RowID
    {
        get => Metadata_SuggestionID;
        set => Metadata_SuggestionID = value;
    }

    Metadata_Suggestion IMetadataStoreRow<Metadata_Suggestion>.Clone()
        => (Metadata_Suggestion)MemberwiseClone();

    #endregion
}
