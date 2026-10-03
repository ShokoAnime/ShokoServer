using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;

namespace Shoko.Plugin.Tmdb;

/// <summary>
/// Configuration for the TMDb plugin, kept in <c>tmdb.json</c> in the
/// plugin's configuration folder.
/// </summary>
/// <remarks>
/// The property names match the server's old <c>TMDB</c> section, which a
/// settings migration moved here as it was. The image server is not here: it
/// is the image template URL for the <c>tmdb</c> source.
/// </remarks>
[Display(Name = "TMDb")]
[StorageLocation(FileName = "tmdb")]
public class TmdbConfiguration : IConfiguration
{
    #region Linking

    /// <summary>
    /// Determines whether to consider existing cross-reference links to other
    /// AniDB anime when linking an AniDB anime to a TMDb show.
    /// </summary>
    /// <remarks>
    /// This setting also applies to the auto-matching process and can be
    /// overridden on a per request basis for the API when previewing or
    /// linking.
    /// </remarks>
    public bool ConsiderExistingOtherLinks { get; set; } = false;

    #endregion

    #region Downloads

    /// <summary>
    /// Indicates that all titles should be stored locally for the TMDb entity,
    /// otherwise the server's series or episode title language order decides
    /// which titles to store locally.
    /// </summary>
    public bool DownloadAllTitles { get; set; } = false;

    /// <summary>
    /// Indicates that all overviews should be stored locally for the TMDb
    /// entity, otherwise the server's description language order decides
    /// which overviews to store locally.
    /// </summary>
    public bool DownloadAllOverviews { get; set; } = false;

    /// <summary>
    /// Indicates that all content-ratings should be stored locally for the TMDb
    /// entity, otherwise the server's series or episode title language order
    /// decides which content-ratings to store locally.
    /// </summary>
    public bool DownloadAllContentRatings { get; set; } = false;

    /// <summary>
    /// Automagically download crew and cast for movies and tv shows in the
    /// local collection.
    /// </summary>
    public bool AutoDownloadCrewAndCast { get; set; } = false;

    /// <summary>
    /// Automagically download collections for movies and tv shows in the local
    /// collection.
    /// </summary>
    public bool AutoDownloadCollections { get; set; } = false;

    /// <summary>
    /// Automagically download episode groups to use with alternate ordering
    /// for tv shows.
    /// </summary>
    public bool AutoDownloadAlternateOrdering { get; set; } = false;

    /// <summary>
    /// Automagically download networks for tv shows in the local collection.
    /// </summary>
    public bool AutoDownloadNetworks { get; set; } = false;

    #endregion

    #region Connection

    /// <summary>
    /// Optional. User provided TMDb API key to use.
    /// </summary>
    [Badge("Advanced", Theme = DisplayColorTheme.Primary)]
    [Visibility(Advanced = true)]
    [EnvironmentVariable("TMDB_API_KEY")]
    [RequiresRestart]
    [PasswordPropertyText]
    public string? UserApiKey { get; set; } = null;

    /// <summary>
    /// The number of days to check for incremental changes. Set to <c>0</c> to
    /// disable incremental changes.
    /// </summary>
    /// <remarks>
    /// The TMDb API covers at most the last 14 days. So we can only use
    /// incremental changes detection for up-to the last 14 days.
    /// </remarks>
    [Visibility(Size = DisplayElementSize.Large)]
    [EnvironmentVariable("TMDB_CHANGES_WINDOW_DAYS")]
    [Range(0, 14)]
    [DefaultValue(1)]
    public int IncrementalChangesWindowDays { get; set; } = 1;

    #endregion

    #region Searching

    /// <summary>
    /// The maximum number of TMDb search candidates to evaluate per auto-search
    /// attempt for shows. Each candidate that passes the animation filter is
    /// fetched in full and scored, and the highest-scoring result is used.
    /// </summary>
    /// <remarks>
    /// Higher values improve accuracy at the cost of more TMDb API calls, which
    /// are paced by the rate limiter, so a higher value slows searches down
    /// rather than tripping TMDb's limits. The year-free candidate pool is
    /// capped at twice this value.
    /// </remarks>
    [Range(1, 10)]
    public int AutoSearchShowCandidateCount { get; set; } = 5;

    /// <summary>
    /// The maximum number of TMDb search candidates to evaluate per auto-search
    /// attempt for movies. Each candidate that passes the animation filter is
    /// fetched in full and scored, and the highest-scoring result is used.
    /// </summary>
    /// <remarks>
    /// Higher values improve accuracy at the cost of more TMDb API calls, which
    /// are paced by the rate limiter, so a higher value slows searches down
    /// rather than tripping TMDb's limits. The year-free candidate pool is
    /// capped at twice this value.
    /// </remarks>
    [Range(1, 10)]
    public int AutoSearchMovieCandidateCount { get; set; } = 5;

    #endregion

    #region Rate Limit

    /// <summary>
    /// Rate limit settings for the TMDb API.
    /// </summary>
    public TmdbRateLimitConfiguration RateLimit { get; set; } = new();

    #endregion
}
