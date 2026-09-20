using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// An entry TMDB suggests to someone looking at another one, either as a
/// recommendation or as a similar title. A show suggests shows and a movie
/// suggests movies, so both ends share <see cref="TmdbEntityType"/>. The
/// suggested entry is usually not in the collection, so only its TMDB ID and
/// its place in TMDB's list are kept.
/// </summary>
public class TMDB_Suggestion : ISuggestedMetadata
{
    #region Database Columns

    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_SuggestionID { get; set; }

    /// <summary>
    /// Whether both ends are shows or movies.
    /// </summary>
    public DataEntityType TmdbEntityType { get; set; }

    /// <summary>
    /// TMDB ID of the entry being looked at.
    /// </summary>
    public int TmdbEntityID { get; set; }

    /// <summary>
    /// TMDB ID of the suggested entry.
    /// </summary>
    public int SuggestedTmdbEntityID { get; set; }

    /// <summary>
    /// Which of TMDB's two lists the suggestion came from.
    /// </summary>
    public SuggestionKind Kind { get; set; }

    /// <summary>
    /// The entry's position in that list, best first, starting at <c>0</c>.
    /// </summary>
    public int Ordering { get; set; }

    #endregion

    #region Methods

    /// <summary>
    /// The entry being looked at, if it is in the collection.
    /// </summary>
    /// <returns>The show or movie, or <c>null</c>.</returns>
    public IMetadata<int>? GetTmdbEntity()
        => GetEntity(TmdbEntityID);

    /// <summary>
    /// The suggested entry, if it is in the collection.
    /// </summary>
    /// <returns>The show or movie, or <c>null</c>.</returns>
    public IMetadata<int>? GetSuggestedTmdbEntity()
        => GetEntity(SuggestedTmdbEntityID);

    private IMetadata<int>? GetEntity(int entityID)
        => TmdbEntityType switch
        {
            DataEntityType.Show => RepoFactory.TMDB_Show.GetByTmdbShowID(entityID),
            DataEntityType.Movie => RepoFactory.TMDB_Movie.GetByTmdbMovieID(entityID),
            _ => null,
        };

    #endregion

    #region ISuggestedMetadata Implementation

    int ISuggestedMetadata.BaseID => TmdbEntityID;

    int ISuggestedMetadata.SuggestedID => SuggestedTmdbEntityID;

    IMetadata<int>? ISuggestedMetadata.Base => GetTmdbEntity();

    IMetadata<int>? ISuggestedMetadata.Suggested => GetSuggestedTmdbEntity();

    SuggestionKind ISuggestedMetadata.Kind => Kind;

    int? ISuggestedMetadata.Order => Ordering;

    // TMDB hands out an ordered list and no votes.
    double? ISuggestedMetadata.ApprovalRating => null;

    int? ISuggestedMetadata.Votes => null;

    DataSource ISuggestedMetadata.Source => DataSource.TMDB;

    public bool Equals(ISuggestedMetadata? other)
        => other is not null && other.Source is DataSource.TMDB && other.Kind == Kind &&
            other.BaseID == TmdbEntityID && other.SuggestedID == SuggestedTmdbEntityID;

    #endregion
}
