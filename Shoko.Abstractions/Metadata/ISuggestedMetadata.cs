using System;
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
    /// Base entity id.
    /// </summary>
    int BaseID { get; }

    /// <summary>
    /// Suggested entity id, in the source's own numbering.
    /// </summary>
    int SuggestedID { get; }

    /// <summary>
    /// Base entity, if available.
    /// </summary>
    IMetadata<int>? Base { get; }

    /// <summary>
    /// Suggested entity, if available. Usually <see langword="null"/>, since
    /// most suggestions point at entities that are not in the collection.
    /// </summary>
    IMetadata<int>? Suggested { get; }

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
    /// <see langword="null"/> when the source only hands out an ordered list.
    /// </summary>
    double? ApprovalRating { get; }

    /// <summary>
    /// The number of votes behind the suggestion, or <see langword="null"/>
    /// when the source does not vote on them.
    /// </summary>
    int? Votes { get; }

    /// <summary>
    /// The source of the suggestion.
    /// </summary>
    DataSource Source { get; }
}

/// <summary>
/// A suggestion with its entities.
/// </summary>
/// <typeparam name="TBaseMetadata">Base entity type.</typeparam>
/// <typeparam name="TSuggestedMetadata">Suggested entity type.</typeparam>
public interface ISuggestedMetadata<out TBaseMetadata, out TSuggestedMetadata> : ISuggestedMetadata
    where TBaseMetadata : IMetadata<int> where TSuggestedMetadata : IMetadata<int>
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
