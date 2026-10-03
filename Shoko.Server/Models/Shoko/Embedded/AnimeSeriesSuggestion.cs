using System;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;

namespace Shoko.Server.Models.Shoko.Embedded;

/// <summary>
///   A suggestion a series linked to a Shoko series makes, based on a Shoko
///   series instead of the linked series. The suggested end and everything
///   else stay as the source gave them.
/// </summary>
/// <param name="suggestion">The linked series' suggestion.</param>
/// <param name="series">
///   The Shoko series to base it on, or <c>null</c> to keep the source's base
///   ID with no entry.
/// </param>
public sealed class AnimeSeriesSuggestion(ISuggestedMetadata<ISeries, ISeries> suggestion, IShokoSeries? series)
    : ISuggestedMetadata<IShokoSeries, ISeries>
{
    #region ISuggestedMetadata Implementation

    /// <inheritdoc />
    public MetadataGuid BaseID => series?.ID ?? suggestion.BaseID;

    /// <inheritdoc />
    public MetadataGuid SuggestedID => suggestion.SuggestedID;

    IMetadata? ISuggestedMetadata.Base => series;

    IMetadata? ISuggestedMetadata.Suggested => suggestion.Suggested;

    /// <inheritdoc />
    public SuggestionKind Kind => suggestion.Kind;

    /// <inheritdoc />
    public int? Order => suggestion.Order;

    /// <inheritdoc />
    public double? ApprovalRating => suggestion.ApprovalRating;

    /// <inheritdoc />
    public int? ApprovalVotes => suggestion.ApprovalVotes;

    /// <inheritdoc />
    public int? Votes => suggestion.Votes;

    /// <inheritdoc />
    public int? Score => suggestion.Score;

    /// <inheritdoc />
    public MetadataSource Source => suggestion.Source;

    /// <inheritdoc />
    public bool Equals(ISuggestedMetadata? other)
        => other is not null &&
            other.Source == Source &&
            other.BaseID == BaseID &&
            other.SuggestedID == SuggestedID &&
            other.Kind == Kind;

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => obj is ISuggestedMetadata other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
        => HashCode.Combine(Source, BaseID, SuggestedID, Kind);

    #endregion

    #region ISuggestedMetadata<IShokoSeries, ISeries> Implementation

    /// <inheritdoc />
    public IShokoSeries? Base => series;

    /// <inheritdoc />
    public ISeries? Suggested => suggestion.Suggested;

    #endregion
}
