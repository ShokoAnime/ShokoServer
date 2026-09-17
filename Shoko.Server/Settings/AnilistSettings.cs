using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.UI.Attributes;
using Shoko.Abstractions.UI.Enums;

#nullable enable
namespace Shoko.Server.Settings;

public class AnilistSettings
{
    /// <summary>
    /// Automagically link AniDB anime to Anilist anime.
    /// </summary>
    public bool AutoLink { get; set; } = false;

    /// <summary>
    /// Automagically link restricted AniDB anime to Anilist anime.
    /// <see cref="AutoLink"/> also needs to be set for this setting to take
    /// effect.
    /// </summary>
    public bool AutoLinkRestricted { get; set; } = true;

    /// <summary>
    /// Determines whether to consider existing links for other series when
    /// automatically linking episodes.
    /// </summary>
    public bool ConsiderExistingOtherLinks { get; set; } = false;

    /// <summary>
    /// Automagically download staff members for anime in the local collection.
    /// </summary>
    public bool AutoDownloadStaff { get; set; } = false;

    /// <summary>
    /// Automagically download characters for anime in the local collection.
    /// </summary>
    public bool AutoDownloadCharacters { get; set; } = false;

    /// <summary>
    /// The base URL or URL template for the image CDN to use.
    /// </summary>
    [Visibility(Size = DisplayElementSize.Large)]
    [Display(Name = "Image CDN URL")]
    [EnvironmentVariable("ANILIST_IMAGE_CDN_URL")]
    [Url]
    public string? ImageCdnUrl { get; set; }

    /// <summary>
    /// Automagically download the cover image for Anilist anime.
    /// </summary>
    public bool AutoDownloadPosters { get; set; } = true;

    /// <summary>
    /// Automagically download the banner image for Anilist anime.
    /// </summary>
    public bool AutoDownloadBanners { get; set; } = true;

    /// <summary>
    /// Automagically download studio information for anime in the local
    /// collection.
    /// </summary>
    public bool AutoDownloadStudios { get; set; } = false;

    /// <summary>
    /// The number of top search results to evaluate when automatically
    /// searching for an Anilist anime to link to an AniDB anime. Set to
    /// <c>1</c> to only consider the first result.
    /// </summary>
    [Range(1, 20)]
    [Visibility(Size = DisplayElementSize.Small)]
    public int AutoSearchCandidateCount { get; set; } = 5;

    /// <summary>
    /// The number of days an Anilist anime can remain without any links to a
    /// Shoko series before it is automatically purged. Set to <c>0</c> to
    /// disable the automatic purging.
    /// </summary>
    [Range(0, 365)]
    [Visibility(Size = DisplayElementSize.Small)]
    public int AutoPurgeUnlinkedAfterDays { get; set; } = 14;

    /// <summary>
    /// Rate limit settings for the Anilist API.
    /// </summary>
    [Visibility(Advanced = true)]
    public AnilistRateLimitSettings RateLimit { get; set; } = new();
}
