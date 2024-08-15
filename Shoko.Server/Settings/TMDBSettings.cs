using System.ComponentModel.DataAnnotations;
using System.Linq;
using Newtonsoft.Json;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.Server;

#nullable enable
namespace Shoko.Server.Settings;

public class TMDBSettings
{
    /// <summary>
    /// Automagically link AniDB anime to TMDB shows and movies.
    /// </summary>
    public bool AutoLink { get; set; } = false;

    /// <summary>
    /// Automagically link restricted AniDB anime to TMDB shows and movies.
    /// <see cref="AutoLink"/> also needs to be set for this setting to take
    /// effect.
    /// </summary>
    public bool AutoLinkRestricted { get; set; } = false;

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
    /// Download and save external IDs for the TMDB entities.
    /// </summary>
    [JsonProperty(nameof(DownloadExternalIDs))]
    public ForeignEntityType[] InternalDownloadExternalIDs
    {
        get => ModelHelper.UnCombineFlags(DownloadExternalIDs).ToArray();
        set => DownloadExternalIDs = value.CombineFlags();
    }

    /// <summary>
    /// Download and save external IDs for the TMDB entities.
    /// </summary>
    [JsonIgnore]
    public ForeignEntityType DownloadExternalIDs { get; set; } = ForeignEntityType.None;

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
    public int MaxAutoStaffImages { get; set; } = 10;

    /// <summary>
    /// Automagically download studio and company images.
    /// </summary>
    public bool AutoDownloadStudioImages { get; set; } = true;

    /// <summary>
    /// Optional. User provided TMDB API key to use.
    /// </summary>
    public string? UserApiKey { get; set; } = null;
}
