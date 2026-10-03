using System;
using System.Diagnostics.CodeAnalysis;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata;

/// <summary>
/// A suggestion from one entity to another, as sourced from a provider's own
/// users or algorithms. Kept apart from <see cref="IRelatedMetadata"/>, which
/// is the authored relation graph: a suggestion is an opinion, it is not
/// symmetric, and it usually points at an entity we know nothing else about.
/// </summary>
public interface ISuggestedMetadata : IEquatable<ISuggestedMetadata>
{
    /// <summary>
    ///   The entry making the suggestion.
    /// </summary>
    MetadataGuid BaseID { get; }

    /// <summary>
    ///   The entry suggested.
    /// </summary>
    MetadataGuid SuggestedID { get; }

    /// <summary>
    /// Base entity, if available.
    /// </summary>
    IMetadata? Base { get; }

    /// <summary>
    /// Suggested entity, if available. Usually <see langword="null"/>, since
    /// most suggestions point at entities that are not in the collection.
    /// </summary>
    IMetadata? Suggested { get; }

    /// <summary>
    /// What the suggestion claims.
    /// </summary>
    SuggestionKind Kind { get; }

    /// <summary>
    /// The source's own ordering, best first, starting at <c>0</c>.
    /// <see langword="null"/> when the source has no ordering of its own, and
    /// <see cref="ApprovalRating"/> is what ranks the suggestions instead.
    /// </summary>
    int? Order { get; }

    /// <summary>
    /// Approval as a percentage, for a source that votes on its suggestions.
    /// Worked out from <see cref="ApprovalVotes"/> and <see cref="Votes"/>
    /// unless the source gives a percentage of its own. <see langword="null"/>
    /// when the source only hands out an ordered list.
    /// </summary>
    double? ApprovalRating { get => HasVotes && Votes > 0 ? ApprovalVotes.Value / (double)Votes.Value * 100 : null; }

    /// <summary>
    /// Whether <see cref="ApprovalRating"/> is known. <see langword="false"/>
    /// when the source does not rate its suggestions that way; TMDB, for
    /// example, only ranks them through <see cref="Order"/>.
    /// </summary>
    [MemberNotNullWhen(true, nameof(ApprovalRating))]
    bool HasApprovalRating { get => ApprovalRating.HasValue; }

    /// <summary>
    /// The number of votes in favour of the suggestion, for a source that
    /// votes on them. <see langword="null"/> when the source does not vote,
    /// or does not say.
    /// </summary>
    int? ApprovalVotes { get; }

    /// <summary>
    /// The total number of votes on the suggestion, or
    /// <see langword="null"/> when the source does not vote on them.
    /// </summary>
    int? Votes { get; }

    /// <summary>
    /// Whether both <see cref="ApprovalVotes"/> and <see cref="Votes"/> are
    /// known. <see langword="false"/> when the source does not vote on its
    /// suggestions, or does not give the counts.
    /// </summary>
    [MemberNotNullWhen(true, nameof(ApprovalVotes), nameof(Votes))]
    bool HasVotes { get => ApprovalVotes.HasValue && Votes.HasValue; }

    /// <summary>
    /// The source's net score for the suggestion, where it keeps one instead
    /// of, or beside, an approval rating. May be negative.
    /// </summary>
    int? Score { get; }

    /// <summary>
    /// Whether <see cref="Score"/> is known. <see langword="false"/> when the
    /// source does not score its suggestions; TMDB, for example, only ranks
    /// them through <see cref="Order"/>.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Score))]
    bool HasScore { get => Score.HasValue; }

    /// <summary>
    /// The source of the suggestion.
    /// </summary>
    MetadataSource Source { get; }
}

/// <summary>
/// A suggestion with its entities.
/// </summary>
/// <typeparam name="TBaseMetadata">Base entity type.</typeparam>
/// <typeparam name="TSuggestedMetadata">Suggested entity type.</typeparam>
public interface ISuggestedMetadata<out TBaseMetadata, out TSuggestedMetadata> : ISuggestedMetadata
    where TBaseMetadata : IMetadata where TSuggestedMetadata : IMetadata
{
    /// <summary>
    /// Base entity, if available.
    /// </summary>
    new TBaseMetadata? Base { get; }

    /// <summary>
    /// Suggested entity, if available.
    /// </summary>
    new TSuggestedMetadata? Suggested { get; }
}
