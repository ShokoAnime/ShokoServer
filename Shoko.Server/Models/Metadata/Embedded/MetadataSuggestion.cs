using System;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Services.MetadataStorage;

namespace Shoko.Server.Models.Metadata.Embedded;

/// <summary>
///   A stored suggestion read as the kinds of entry asked for.
/// </summary>
/// <typeparam name="TBase">The kind of entry making the suggestion.</typeparam>
/// <typeparam name="TSuggested">The kind of entry suggested.</typeparam>
/// <param name="row">The stored suggestion.</param>
public sealed class MetadataSuggestion<TBase, TSuggested>(Metadata_Suggestion row) : ISuggestedMetadata<TBase, TSuggested>
    where TBase : IMetadata
    where TSuggested : IMetadata
{
    #region ISuggestedMetadata Implementation

    /// <inheritdoc />
    public MetadataSource Source => row.Source;

    /// <inheritdoc />
    public MetadataGuid BaseID => new(row.Source, row.BaseType, row.BaseID);

    /// <inheritdoc />
    public MetadataGuid SuggestedID => new(row.Source, row.SuggestedType, row.SuggestedID);

    /// <inheritdoc />
    public SuggestionKind Kind => row.Kind;

    /// <inheritdoc />
    public int? Order => row.Ranking;

    /// <inheritdoc />
    public double? ApprovalRating => row.ApprovalRating;

    /// <inheritdoc />
    public int? Votes => row.Votes;

    /// <inheritdoc />
    public int? Score => row.Score;

    /// <inheritdoc />
    public TBase? Base => MetadataEntries.Resolve(BaseID) is TBase entry ? entry : default;

    /// <inheritdoc />
    public TSuggested? Suggested => MetadataEntries.Resolve(SuggestedID) is TSuggested entry ? entry : default;

    IMetadata? ISuggestedMetadata.Base => MetadataEntries.Resolve(BaseID);

    IMetadata? ISuggestedMetadata.Suggested => MetadataEntries.Resolve(SuggestedID);

    #endregion

    #region Equality

    /// <inheritdoc />
    public bool Equals(ISuggestedMetadata? other)
        => other is not null &&
            other.Source == row.Source &&
            other.BaseID == BaseID &&
            other.SuggestedID == SuggestedID &&
            other.Kind == row.Kind;

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => obj is ISuggestedMetadata other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
        => HashCode.Combine(row.Source, BaseID, SuggestedID, row.Kind);

    #endregion
}
