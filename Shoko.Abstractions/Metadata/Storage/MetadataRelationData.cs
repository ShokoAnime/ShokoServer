using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Storage;

/// <summary>
///   An authored relation from one entry to another on the same source.
/// </summary>
public sealed record MetadataRelationData
{
    /// <summary>
    ///   The related entry, which may be of another kind.
    /// </summary>
    public required MetadataGuid RelatedID { get; init; }

    /// <summary>
    ///   What the related entry is to this one.
    /// </summary>
    public required RelationType RelationType { get; init; }
}

/// <summary>
///   A suggestion from one entry to another on the same source.
/// </summary>
public sealed record MetadataSuggestionData
{
    /// <summary>
    ///   The suggested entry, which may be of another kind.
    /// </summary>
    public required MetadataGuid SuggestedID { get; init; }

    /// <summary>
    ///   Whether it is a recommendation or a likeness.
    /// </summary>
    public SuggestionKind Kind { get; init; } = SuggestionKind.Recommended;

    /// <summary>
    ///   The source's own ranking, best first from 0, when it ranks.
    /// </summary>
    public int? Order { get; init; }

    /// <summary>
    ///   The percentage of voters who approve, when the source votes.
    /// </summary>
    public double? ApprovalRating { get; init; }

    /// <summary>
    ///   How many voted, when the source says.
    /// </summary>
    public int? Votes { get; init; }

    /// <summary>
    ///   The source's net score, when it keeps one. May be negative.
    /// </summary>
    public int? Score { get; init; }
}
