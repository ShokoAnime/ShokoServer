using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Search;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;

using AbstractSearchResult = Shoko.Abstractions.Metadata.Search.MetadataSearchResult;

namespace Shoko.Server.API.v3.Models.Metadata;

/// <summary>
/// A series or movie a provider found, whether or not it is stored.
/// </summary>
public class MetadataSearchResult
{
    /// <summary>
    /// Describes what a provider found.
    /// </summary>
    /// <param name="result">The provider's answer.</param>
    /// <param name="isLocal">Whether the entry is stored already.</param>
    /// <param name="siteUrl">The entry's own page on its source's site, if it has one.</param>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <see langword="null"/>.</exception>
    public MetadataSearchResult(AbstractSearchResult result, bool isLocal, string? siteUrl)
    {
        ArgumentNullException.ThrowIfNull(result);

        ID = result.ID.ID;
        Source = result.ID.Source;
        Type = result.ID.EntityType;
        Guid = result.ID.ToString();
        SiteUrl = siteUrl;
        Title = result.Title;
        OriginalTitle = result.OriginalTitle;
        OriginalLanguage = result.OriginalLanguageCode;
        Overview = result.Overview;
        IsRestricted = result.IsRestricted;
        Rating = result.UserRating is { } rating ? MetadataModelBuilder.Rating(result.Source, (double)rating, result.UserVotes ?? 0) : null;
        PosterUrl = result.PosterUrl;
        BackdropUrl = result.BackdropUrl;
        Genres = result.Genres;
        IsLocal = isLocal;
        switch (result)
        {
            case MetadataSeriesSearchResult series:
                AirDate = series.FirstAiredAt;
                AnimeType = series.Type;
                Season = series.Season;
                SeasonYear = series.SeasonYear;
                EpisodeCount = series.EpisodeCount;
                break;
            case MetadataMovieSearchResult movie:
                ReleaseDate = movie.ReleasedAt;
                IsVideo = movie.IsStandaloneVideo;
                break;
        }
    }

    #region Identity

    /// <summary>
    /// The source's own ID for the entry.
    /// </summary>
    [Required]
    public string ID { get; init; }

    /// <summary>
    /// The source the entry belongs to.
    /// </summary>
    [Required]
    public MetadataSource Source { get; init; }

    /// <summary>
    /// Whether the entry is a series or a movie.
    /// </summary>
    [Required]
    public MetadataEntityType Type { get; init; }

    /// <summary>
    /// The entry's full identifier, e.g. <c>anilist://series/21</c>.
    /// </summary>
    [Required]
    public string Guid { get; init; }

    /// <summary>
    /// The entry's own page on its source's site, or <c>null</c> when it has
    /// none.
    /// </summary>
    public string? SiteUrl { get; init; }

    /// <summary>
    /// Whether the entry is stored already, so its full details can be read
    /// through the <c>Metadata/{source}</c> routes.
    /// </summary>
    [Required]
    public bool IsLocal { get; init; }

    #endregion

    #region Details

    /// <summary>
    /// The title, in the language the provider answered in.
    /// </summary>
    [Required]
    public string Title { get; init; }

    /// <summary>
    /// The title in the entry's original language, if known.
    /// </summary>
    public string? OriginalTitle { get; init; }

    /// <summary>
    /// The entry's original language, as a language code, if known.
    /// </summary>
    public string? OriginalLanguage { get; init; }

    /// <summary>
    /// The overview, if any.
    /// </summary>
    public string? Overview { get; init; }

    /// <summary>
    /// Whether the entry is restricted to adults.
    /// </summary>
    [Required]
    public bool IsRestricted { get; init; }

    /// <summary>
    /// The community rating, on a scale of 1 to 10, if any.
    /// </summary>
    public Rating? Rating { get; init; }

    /// <summary>
    /// A link to the poster, if any.
    /// </summary>
    public string? PosterUrl { get; init; }

    /// <summary>
    /// A link to the backdrop, if any.
    /// </summary>
    public string? BackdropUrl { get; init; }

    /// <summary>
    /// The genres, by name.
    /// </summary>
    [Required]
    public IReadOnlyList<string> Genres { get; init; }

    #endregion

    #region Series

    /// <summary>
    /// When a series first aired, if known.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public PartialDateOnly? AirDate { get; init; }

    /// <summary>
    /// What kind of release a series is.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore), JsonConverter(typeof(StringEnumConverter))]
    public AnimeType? AnimeType { get; init; }

    /// <summary>
    /// The season of the year a series first aired in, if known.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore), JsonConverter(typeof(StringEnumConverter))]
    public YearlySeason? Season { get; init; }

    /// <summary>
    /// The year of <see cref="Season"/>, if known.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? SeasonYear { get; init; }

    /// <summary>
    /// How many episodes a series has, if known.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? EpisodeCount { get; init; }

    #endregion

    #region Movie

    /// <summary>
    /// When a movie was released, if known.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public PartialDateOnly? ReleaseDate { get; init; }

    /// <summary>
    /// Whether a movie was released on its own as a video rather than in
    /// cinemas.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsVideo { get; init; }

    #endregion
}

/// <summary>
/// A candidate an automatic search scored for an AniDB anime or episode,
/// taken or turned down.
/// </summary>
public class MetadataAutoMatchResult
{
    /// <summary>
    /// Describes a match.
    /// </summary>
    /// <param name="candidate">The match.</param>
    /// <param name="isLocal">Whether the matched entry is stored already.</param>
    /// <param name="siteUrl">The matched entry's own page on its source's site, if it has one.</param>
    /// <exception cref="ArgumentNullException"><paramref name="candidate"/> is <see langword="null"/>.</exception>
    public MetadataAutoMatchResult(MetadataAutoLinkCandidate candidate, bool isLocal, string? siteUrl)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        ID = candidate.ID.ID;
        AnidbAnimeID = candidate.AnidbAnimeID;
        AnidbEpisodeID = candidate.AnidbEpisodeID;
        MatchRating = candidate.MatchRating;
        IsLocal = candidate.IsLocal || isLocal;
        IsRemote = candidate.IsRemote;
        Origin = candidate.Origin;
        LinkMatchRating = candidate.LinkMatchRating;
        PrequelAnidbAnimeID = candidate.PrequelAnidbAnimeID;
        Result = new(candidate.Result, IsLocal, siteUrl);
        Rejection = candidate.Rejection is { } rejection ? new(rejection) : null;
    }

    /// <summary>
    /// The source's own ID for the matched entry.
    /// </summary>
    [Required]
    public string ID { get; init; }

    /// <summary>
    /// The AniDB anime the match is for.
    /// </summary>
    [Required]
    public int AnidbAnimeID { get; init; }

    /// <summary>
    /// The AniDB episode a movie match is for, if it is one.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? AnidbEpisodeID { get; init; }

    /// <summary>
    /// How the match was arrived at. <c>None</c> for a prequel's link, whose
    /// rating is the prequel's and is given in <see cref="LinkMatchRating"/>.
    /// </summary>
    [Required, JsonConverter(typeof(StringEnumConverter))]
    public MatchRating MatchRating { get; init; }

    /// <summary>
    /// Where the candidate came from: the search, the anime's current links, a
    /// prequel's links, the anime's AniDB resources, or its links on other
    /// sources. An automatic search links search results, and the first hint of
    /// the last two only when the anime has no link on the source and no
    /// competing search result rates as high; the rest are context. The same
    /// entry may appear once per origin.
    /// </summary>
    [Required, JsonConverter(typeof(StringEnumConverter))]
    public MetadataAutoLinkOrigin Origin { get; init; }

    /// <summary>
    /// The rating of the stored link a current or prequel link was listed
    /// from, for context only.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore), JsonConverter(typeof(StringEnumConverter))]
    public MatchRating? LinkMatchRating { get; init; }

    /// <summary>
    /// The AniDB anime whose link a prequel link is.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? PrequelAnidbAnimeID { get; init; }

    /// <summary>
    /// Whether the match was found among the stored entries.
    /// </summary>
    [Required]
    public bool IsLocal { get; init; }

    /// <summary>
    /// Whether the match was found by asking the provider.
    /// </summary>
    [Required]
    public bool IsRemote { get; init; }

    /// <summary>
    /// The matched entry.
    /// </summary>
    [Required]
    public MetadataSearchResult Result { get; init; }

    /// <summary>
    /// Why the candidate is not linked, or <see langword="null"/> when an
    /// automatic search links it. A candidate turned down can still be linked
    /// by hand.
    /// </summary>
    public MetadataAutoLinkRejectionResult? Rejection { get; init; }
}

/// <summary>
/// Why an automatic search did not link a candidate.
/// </summary>
public class MetadataAutoLinkRejectionResult
{
    /// <summary>
    /// Describes a rejection.
    /// </summary>
    /// <param name="rejection">The rejection.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rejection"/> is <see langword="null"/>.</exception>
    public MetadataAutoLinkRejectionResult(MetadataAutoLinkRejection rejection)
    {
        ArgumentNullException.ThrowIfNull(rejection);

        Reason = rejection.Reason;
        Details = rejection.Details;
    }

    /// <summary>
    /// The reason that decided it.
    /// </summary>
    [Required, JsonConverter(typeof(StringEnumConverter))]
    public MatchRejectionReason Reason { get; init; }

    /// <summary>
    /// Anything more worth showing about it, if anything.
    /// </summary>
    public string? Details { get; init; }
}
