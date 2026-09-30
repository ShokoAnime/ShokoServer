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
    /// The source that made the suggestion.
    /// </summary>
    [Required]
    public MetadataSource Source { get; set; }

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

    /// <summary>
    /// The source's net score for the suggestion, where it keeps one. May be
    /// negative.
    /// </summary>
    public int? Score { get; set; }

    public SeriesSuggestion(ISuggestedMetadata suggestion)
    {
        IDs = SuggestionIDs.FromEntry(suggestion.BaseID);
        SuggestedIDs = SuggestionIDs.FromEntry(suggestion.SuggestedID);
        Kind = suggestion.Kind;
        Source = suggestion.Source;
        Order = suggestion.Order;
        ApprovalRating = suggestion.ApprovalRating;
        Votes = suggestion.Votes;
        Score = suggestion.Score;
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
        /// The source's own ID, for a source other than AniDB and TMDB.
        /// </summary>
        public string? Provider { get; set; }

        /// <summary>
        /// Builds the IDs for one end of a suggestion, tracing the provider's
        /// own ID back to a local series where a cross-reference exists.
        /// </summary>
        /// <param name="entry">
        /// The entry at this end. AniDB and TMDB use numbers, so for them one
        /// that is not a number yields no IDs.
        /// </param>
        /// <returns>The IDs.</returns>
        public static SuggestionIDs FromEntry(MetadataGuid entry)
        {
            var ids = new SuggestionIDs();
            var source = entry.Source;
            switch (source)
            {
                case var _ when (source == MetadataSource.AniDB || source == MetadataSource.TMDB) && !entry.TryGetNumericID(out int _):
                    return ids;

                case var _ when source == MetadataSource.AniDB:
                    ids.AniDB = entry.GetNumericID<int>();
                    break;

                case var _ when source == MetadataSource.TMDB && entry.EntityType == MetadataEntityType.Movie:
                    ids.TmdbMovie = entry.GetNumericID<int>();
                    ids.AniDB = RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByTmdbMovieID(ids.TmdbMovie.Value)
                        .FirstOrDefault()?.AnidbAnimeID;
                    break;

                case var _ when source == MetadataSource.TMDB:
                    ids.TmdbShow = entry.GetNumericID<int>();
                    ids.AniDB = RepoFactory.CrossRef_AniDB_TMDB_Show.GetByTmdbShowID(ids.TmdbShow.Value)
                        .FirstOrDefault()?.AnidbAnimeID;
                    break;

                default:
                    ids.Provider = entry.ID;
                    ids.AniDB = RepoFactory.CrossRef_AniDB_Metadata_Series.GetByProviderID(source, entry.ID)
                        .FirstOrDefault()?.AnidbAnimeID;
                    break;
            }

            if (ids.AniDB is { } anidbAnimeID)
                ids.Shoko = RepoFactory.AnimeSeries.GetByAnimeID(anidbAnimeID)?.AnimeSeriesID;

            return ids;
        }
    }
}
