using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.Settings;

public class TMDBSettings
{
    /// <summary>
    /// Automagically link AniDB anime to TMDB shows and movies.
    /// </summary>
    public bool AutoLink { get; set; } = true;

    /// <summary>
    /// Automagically link restricted AniDB anime to TMDB shows and movies.
    /// <see cref="AutoLink"/> also needs to be set for this setting to take
    /// effect.
    /// </summary>
    public bool AutoLinkRestricted { get; set; } = true;

    /// <summary>
    /// Determines whether to consider existing cross-reference links to other
    /// AniDB anime when linking an AniDB anime to a TMDB show.
    /// </summary>
    /// <remarks>
    /// This setting also applies to the auto-matching process and can be
    /// overridden on a per request basis for the API when previewing or
    /// linking.
    /// </remarks>
    public bool ConsiderExistingOtherLinks { get; set; } = false;

    /// <summary>
    /// Indicates that all titles should be stored locally for the TMDB entity,
    /// otherwise it will use
    /// <seealso cref="LanguageSettings.SeriesTitleLanguageOrder"/> or
    /// <seealso cref="LanguageSettings.EpisodeTitleLanguageOrder"/> depending
    /// on the entity type to determine which titles to store locally.
    /// </summary>
    public bool DownloadAllTitles { get; set; } = false;

    /// <summary>
    /// Indicates that all overviews should be stored locally for the TMDB
    /// entity, otherwise it will use
    /// <seealso cref="LanguageSettings.DescriptionLanguageOrder"/> to determine
    /// which overviews should be stored locally.
    /// </summary>
    public bool DownloadAllOverviews { get; set; } = false;

    /// <summary>
    /// Indicates that all content-ratings should be stored locally for the TMDB
    /// entity, otherwise it will use
    /// <seealso cref="LanguageSettings.SeriesTitleLanguageOrder"/> or
    /// <seealso cref="LanguageSettings.EpisodeTitleLanguageOrder"/> depending
    /// on the entity type to determine which content-ratings to store locally.
    /// </summary>
    public bool DownloadAllContentRatings { get; set; } = false;

    /// <summary>
    /// Image language preference order, in text form for storage.
    /// </summary>
    [Display(Name = "Image Language Order")]
    [JsonProperty(nameof(ImageLanguageOrder))]
    [UsedImplicitly]
    public List<string> InternalImageLanguageOrder
    {
        get => ImageLanguageOrder
            .Select(x => x.GetString())
            .ToList();
        set => ImageLanguageOrder = value
            .Select(x => x.GetTitleLanguage())
            .Distinct()
            .Where(x => x is not TitleLanguage.Unknown)
            .ToList();
    }

    /// <summary>
    /// Image language preference order, as enum values for consumption.
    /// </summary>
    [JsonIgnore]
    public List<TitleLanguage> ImageLanguageOrder { get; set; } = [TitleLanguage.None, TitleLanguage.Main, TitleLanguage.English];

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

    /// <summary>
    /// Automagically download backdrops for TMDB entities that supports
    /// backdrops up to <seealso cref="MaxAutoBackdrops"/> images per entity.
    /// </summary>
    public bool AutoDownloadBackdrops { get; set; } = true;

    /// <summary>
    /// The maximum number of backdrops to download for each TMDB entity that
    /// supports backdrops.
    /// </summary>
    /// <remarks>
    /// Set to <code>0</code> to disable the limit.
    /// </remarks>
    [Range(0, 30)]
    [Visibility(
        Size = DisplayElementSize.Small,
        DisableWhenMemberIsSet = nameof(AutoDownloadBackdrops),
        DisableWhenSetTo = false
    )]
    public int MaxAutoBackdrops { get; set; } = 10;

    /// <summary>
    /// Automagically download posters for TMDB entities that supports
    /// posters up to <seealso cref="MaxAutoPosters"/> images per entity.
    /// </summary>
    public bool AutoDownloadPosters { get; set; } = true;

    /// <summary>
    /// The maximum number of posters to download for each TMDB entity that
    /// supports posters.
    /// </summary>
    /// <remarks>
    /// Set to <code>0</code> to disable the limit.
    /// </remarks>
    [Range(0, 30)]
    [Visibility(
        Size = DisplayElementSize.Small,
        DisableWhenMemberIsSet = nameof(AutoDownloadPosters),
        DisableWhenSetTo = false
    )]
    public int MaxAutoPosters { get; set; } = 10;

    /// <summary>
    /// Automagically download logos for TMDB entities that supports
    /// logos up to <seealso cref="MaxAutoLogos"/> images per entity.
    /// </summary>
    public bool AutoDownloadLogos { get; set; } = true;

    /// <summary>
    /// The maximum number of logos to download for each TMDB entity that
    /// supports logos.
    /// </summary>
    /// <remarks>
    /// Set to <code>0</code> to disable the limit.
    /// </remarks>
    [Range(0, 30)]
    [Visibility(
        Size = DisplayElementSize.Small,
        DisableWhenMemberIsSet = nameof(AutoDownloadLogos),
        DisableWhenSetTo = false
    )]
    public int MaxAutoLogos { get; set; } = 10;

    /// <summary>
    /// Automagically download thumbnail images for TMDB entities that supports
    /// thumbnails.
    /// </summary>
    public bool AutoDownloadThumbnails { get; set; } = true;

    /// <summary>
    /// The maximum number of thumbnail images to download for each TMDB entity
    /// that supports thumbnail images.
    /// </summary>
    /// <remarks>
    /// Set to <code>0</code> to disable the limit.
    /// </remarks>
    [Range(0, 30)]
    [Visibility(
        Size = DisplayElementSize.Small,
        DisableWhenMemberIsSet = nameof(AutoDownloadThumbnails),
        DisableWhenSetTo = false
    )]
    public int MaxAutoThumbnails { get; set; } = 1;

    /// <summary>
    /// Automagically download staff member and voice-actor images.
    /// </summary>
    public bool AutoDownloadStaffImages { get; set; } = true;

    /// <summary>
    /// The maximum number of staff member and voice-actor images to download
    /// for each TMDB entity that supports staff member and voice-actor images.
    /// </summary>
    /// <remarks>
    /// Set to <code>0</code> to disable the limit.
    /// </remarks>
    [Range(0, 30)]
    [Visibility(
        Size = DisplayElementSize.Small,
        DisableWhenMemberIsSet = nameof(AutoDownloadStaffImages),
        DisableWhenSetTo = false
    )]
    public int MaxAutoStaffImages { get; set; } = 10;

    /// <summary>
    /// Automagically download studio and company images.
    /// </summary>
    public bool AutoDownloadStudioImages { get; set; } = true;

    /// <summary>
    /// Optional. User provided TMDB API key to use.
    /// </summary>
    [Badge("Advanced", Theme = DisplayColorTheme.Primary)]
    [Visibility(Advanced = true)]
    [EnvironmentVariable("TMDB_API_KEY")]
    [RequiresRestart]
    [PasswordPropertyText]
    public string? UserApiKey { get; set; } = null;

    /// <summary>
    /// The base URL or URL template for the image CDN to use.
    /// </summary>
    [Visibility(Size = DisplayElementSize.Large)]
    [Display(Name = "Image CDN URL")]
    [EnvironmentVariable("TMDB_IMAGE_CDN_URL")]
    [Url]
    public string? ImageCdnUrl { get; set; }

    /// <summary>
    /// The number of days to check for incremental changes. Set to <c>0</c> to
    /// disable incremental changes.
    /// </summary>
    /// <remarks>
    /// The TMDB API covers at most the last 14 days. So we can only use
    /// incremental changes detection for up-to the last 14 days.
    /// </remarks>
    [Visibility(Size = DisplayElementSize.Large)]
    [EnvironmentVariable("TMDB_CHANGES_WINDOW_DAYS")]
    [Range(0, 14)]
    [DefaultValue(1)]
    public int IncrementalChangesWindowDays { get; set; } = 1;

    /// <summary>
    /// The maximum number of TMDB search candidates to evaluate per auto-search
    /// attempt for shows. Each candidate that passes the animation filter is fetched
    /// in full (title translations + episode count) and scored; the highest-scoring
    /// result is used. Higher values improve accuracy at the cost of more TMDB
    /// API calls. All calls are paced by <c>TmdbRateLimiter</c> (sliding-window,
    /// ~40 req/sec) with automatic 429 backoff, so increasing this value slows
    /// searches but will not trigger rate-limit errors.
    /// <para>
    /// The year-free candidate pool cap is <c>2 × this value</c>. At the minimum
    /// of 1, year-free searches collect at most 2 candidates total — sufficient for
    /// most titles, but edge cases may benefit from a higher value.
    /// </para>
    /// </summary>
    [Range(1, 10)]
    public int AutoSearchShowCandidateCount { get; set; } = 5;

    /// <summary>
    /// The maximum number of TMDB search candidates to evaluate per auto-search
    /// attempt for movies. Each candidate that passes the animation filter is fetched
    /// in full (title translations + release dates) and scored; the highest-scoring
    /// result is used. Higher values improve accuracy at the cost of more TMDB
    /// API calls. All calls are paced by <c>TmdbRateLimiter</c> (sliding-window,
    /// ~40 req/sec) with automatic 429 backoff, so increasing this value slows
    /// searches but will not trigger rate-limit errors.
    /// <para>
    /// The year-free candidate pool cap is <c>2 × this value</c>. At the minimum
    /// of 1, year-free searches collect at most 2 candidates total — sufficient for
    /// most titles, but edge cases may benefit from a higher value.
    /// </para>
    /// </summary>
    [Range(1, 10)]
    public int AutoSearchMovieCandidateCount { get; set; } = 5;

    /// <summary>
    /// Rate limit settings for the TMDB API.
    /// </summary>
    public TmdbRateLimitSettings RateLimit { get; set; } = new();

    /// <summary>
    /// Number of days a TMDB show or movie can remain in the local database
    /// without any AniDB cross-reference before it is automatically purged.
    /// Set to <c>0</c> to disable automatic purging.
    /// </summary>
    [Visibility(Size = DisplayElementSize.Large)]
    [EnvironmentVariable("TMDB_AUTO_PURGE_UNLINKED_AFTER_DAYS")]
    [Range(0, 365)]
    [DefaultValue(14)]
    public int AutoPurgeUnlinkedAfterDays { get; set; } = 14;
}
