using System.ComponentModel.DataAnnotations;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.API.v3.Models.Common;

/// <summary>
/// A series a provider's users suggest to someone looking at another series,
/// either as a recommendation or as a similar title. Most suggestions point at
/// series that are not in the collection, so the IDs are what is always there
/// and <see cref="SuggestionIDs.Shoko"/> is what usually is not.
/// </summary>
public class SeriesSuggestion
{
    /// <summary>
    /// The IDs of the series being looked at.
    /// </summary>
    [Required]
    public SuggestionIDs IDs { get; set; }

    /// <summary>
    /// The IDs of the suggested series.
    /// </summary>
    [Required]
    public SuggestionIDs SuggestedIDs { get; set; }

    /// <summary>
    /// Whether the suggestion is a recommendation or a similarity.
    /// </summary>
    [Required]
    [JsonConverter(typeof(StringEnumConverter))]
    public SuggestionKind Kind { get; set; }

    /// <summary>
    /// AniDB, TMDB, AniList.
    /// </summary>
    [Required]
    [JsonConverter(typeof(StringEnumConverter))]
    public DataSource Source { get; set; }

    /// <summary>
    /// The source's own ordering, best first, starting at <c>0</c>. Null when
    /// the source ranks by <see cref="ApprovalRating"/> instead.
    /// </summary>
    public int? Order { get; set; }

    /// <summary>
    /// Approval as a percentage, for a source that votes on its suggestions.
    /// </summary>
    public double? ApprovalRating { get; set; }

    /// <summary>
    /// The number of votes behind the suggestion, where the source has them.
    /// </summary>
    public int? Votes { get; set; }

    public SeriesSuggestion(ISuggestedMetadata suggestion, DataEntityType entityType = DataEntityType.Show)
    {
        IDs = SuggestionIDs.FromSource(suggestion.Source, suggestion.BaseID, entityType);
        SuggestedIDs = SuggestionIDs.FromSource(suggestion.Source, suggestion.SuggestedID, entityType);
        Kind = suggestion.Kind;
        Source = suggestion.Source;
        Order = suggestion.Order;
        ApprovalRating = suggestion.ApprovalRating;
        Votes = suggestion.Votes;
    }

    /// <summary>
    /// Suggestion IDs. Which of the provider IDs is set depends on the source,
    /// and the AniDB and Shoko IDs are filled in when the entry can be traced
    /// back to a series in the collection.
    /// </summary>
    public class SuggestionIDs
    {
        /// <summary>
        /// The ID of the Shoko series, when it is in the collection.
        /// </summary>
        public int? Shoko { get; set; }

        /// <summary>
        /// The ID of the AniDB anime.
        /// </summary>
        public int? AniDB { get; set; }

        /// <summary>
        /// The ID of the TMDB show.
        /// </summary>
        public int? TmdbShow { get; set; }

        /// <summary>
        /// The ID of the TMDB movie.
        /// </summary>
        public int? TmdbMovie { get; set; }

        /// <summary>
        /// The ID of the AniList anime.
        /// </summary>
        public int? AniList { get; set; }

        /// <summary>
        /// Builds the IDs for one end of a suggestion, tracing the provider's
        /// own ID back to a local series where a cross-reference exists.
        /// </summary>
        /// <param name="source">The provider the suggestion came from.</param>
        /// <param name="id">The provider's ID for this end.</param>
        /// <param name="entityType">
        /// For TMDB, whether the ID is a show or a movie. Ignored for the
        /// other sources, which only deal in anime.
        /// </param>
        /// <returns>The IDs.</returns>
        public static SuggestionIDs FromSource(DataSource source, int id, DataEntityType entityType = DataEntityType.Show)
        {
            var ids = new SuggestionIDs();
            switch (source)
            {
                case DataSource.AniDB:
                    ids.AniDB = id;
                    break;

                case DataSource.TMDB when entityType is DataEntityType.Movie:
                    ids.TmdbMovie = id;
                    ids.AniDB = RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByTmdbMovieID(id)
                        .FirstOrDefault()?.AnidbAnimeID;
                    break;

                case DataSource.TMDB:
                    ids.TmdbShow = id;
                    ids.AniDB = RepoFactory.CrossRef_AniDB_TMDB_Show.GetByTmdbShowID(id)
                        .FirstOrDefault()?.AnidbAnimeID;
                    break;

                case DataSource.AniList:
                    ids.AniList = id;
                    ids.AniDB = RepoFactory.CrossRef_AniDB_Anilist_Anime.GetByAnilistAnimeID(id)
                        .FirstOrDefault()?.AnidbAnimeID;
                    break;
            }

            if (ids.AniDB is { } anidbAnimeID)
                ids.Shoko = RepoFactory.AnimeSeries.GetByAnimeID(anidbAnimeID)?.AnimeSeriesID;

            return ids;
        }
    }
}
