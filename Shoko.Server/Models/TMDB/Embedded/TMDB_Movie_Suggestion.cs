using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Tmdb;

#nullable enable
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// A stored <see cref="TMDB_Suggestion"/> between two movies, typed on both
/// ends. Never persisted; the row behind it is.
/// </summary>
public class TMDB_Movie_Suggestion(TMDB_Suggestion suggestion) : ITmdbMovieSuggestion
{
    private readonly TMDB_Suggestion _suggestion = suggestion;

    #region ISuggestedMetadata Implementation

    /// <inheritdoc/>
    public int BaseID => _suggestion.TmdbEntityID;

    /// <inheritdoc/>
    public int SuggestedID => _suggestion.SuggestedTmdbEntityID;

    MetadataGuid ISuggestedMetadata.BaseID => new(MetadataSource.TMDB, MetadataEntityType.Movie, BaseID.ToString());

    MetadataGuid ISuggestedMetadata.SuggestedID => new(MetadataSource.TMDB, MetadataEntityType.Movie, SuggestedID.ToString());

    IMetadata? ISuggestedMetadata.Base => Base;

    IMetadata? ISuggestedMetadata.Suggested => Suggested;

    /// <inheritdoc/>
    public SuggestionKind Kind => _suggestion.Kind;

    /// <inheritdoc/>
    public int? Order => _suggestion.Ordering;

    /// <inheritdoc/>
    public double? ApprovalRating => null;

    /// <inheritdoc/>
    public int? Votes => null;

    /// <inheritdoc/>
    public MetadataSource Source => MetadataSource.TMDB;

    /// <inheritdoc/>
    public bool Equals(ISuggestedMetadata? other)
        => ((ISuggestedMetadata)_suggestion).Equals(other);

    #endregion

    #region ISuggestedMetadata<ITmdbMovie> Implementation

    /// <inheritdoc/>
    public ITmdbMovie? Base => _suggestion.GetTmdbEntity() as TMDB_Movie;

    /// <inheritdoc/>
    public ITmdbMovie? Suggested => _suggestion.GetSuggestedTmdbEntity() as TMDB_Movie;

    #endregion
}
