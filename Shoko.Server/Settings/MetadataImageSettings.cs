using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.UI.Attributes;
using Shoko.Abstractions.UI.Enums;

namespace Shoko.Server.Settings;

/// <summary>
/// Which images to download for a metadata source that has a provider, per
/// image type.
/// </summary>
public class MetadataImageSettings
{
    #region Languages

    /// <summary>
    /// Image language preference order, in text form for storage. "main" is
    /// the entry's original language, and "none" an image without text.
    /// </summary>
    [Display(Name = "Image Language Order")]
    [JsonProperty(nameof(ImageLanguageOrder))]
    [UsedImplicitly]
    public List<string> InternalImageLanguageOrder
    {
        get;
        set => field = value
            .Select(x => x.GetTitleLanguage())
            .Where(x => x is not TitleLanguage.Unknown)
            .Distinct()
            .Select(x => x.GetString())
            .ToList();
    } =
    [
        TitleLanguage.None.GetString(), TitleLanguage.Main.GetString(), TitleLanguage.English.GetString()
    ];

    /// <summary>
    /// Image language preference order, as enum values for consumption.
    /// </summary>
    [JsonIgnore]
    public List<TitleLanguage> ImageLanguageOrder => InternalImageLanguageOrder
        .Select(x => x.GetTitleLanguage())
        .Where(x => x is not TitleLanguage.Unknown)
        .Distinct()
        .ToList();

    #endregion

    #region Image Types

    /// <summary>
    /// Automatically download backdrops, up to <see cref="MaxAutoBackdrops"/>
    /// per entity.
    /// </summary>
    public bool AutoDownloadBackdrops { get; set; } = true;

    /// <summary>
    /// The maximum number of backdrops to download for each entity. Set to
    /// <c>0</c> to disable the limit.
    /// </summary>
    [Range(0, 30)]
    [Visibility(Size = DisplayElementSize.Small, DisableWhenMemberIsSet = nameof(AutoDownloadBackdrops), DisableWhenSetTo = false)]
    public int MaxAutoBackdrops { get; set; } = 10;

    /// <summary>
    /// Automatically download posters, up to <see cref="MaxAutoPosters"/> per
    /// entity.
    /// </summary>
    public bool AutoDownloadPosters { get; set; } = true;

    /// <summary>
    /// The maximum number of posters to download for each entity. Set to
    /// <c>0</c> to disable the limit.
    /// </summary>
    [Range(0, 30)]
    [Visibility(Size = DisplayElementSize.Small, DisableWhenMemberIsSet = nameof(AutoDownloadPosters), DisableWhenSetTo = false)]
    public int MaxAutoPosters { get; set; } = 10;

    /// <summary>
    /// Automatically download logos, up to <see cref="MaxAutoLogos"/> per
    /// entity.
    /// </summary>
    public bool AutoDownloadLogos { get; set; } = true;

    /// <summary>
    /// The maximum number of logos to download for each entity. Set to
    /// <c>0</c> to disable the limit.
    /// </summary>
    [Range(0, 30)]
    [Visibility(Size = DisplayElementSize.Small, DisableWhenMemberIsSet = nameof(AutoDownloadLogos), DisableWhenSetTo = false)]
    public int MaxAutoLogos { get; set; } = 10;

    /// <summary>
    /// Automatically download banners, up to <see cref="MaxAutoBanners"/> per
    /// entity.
    /// </summary>
    public bool AutoDownloadBanners { get; set; } = true;

    /// <summary>
    /// The maximum number of banners to download for each entity. Set to
    /// <c>0</c> to disable the limit.
    /// </summary>
    [Range(0, 30)]
    [Visibility(Size = DisplayElementSize.Small, DisableWhenMemberIsSet = nameof(AutoDownloadBanners), DisableWhenSetTo = false)]
    public int MaxAutoBanners { get; set; } = 10;

    /// <summary>
    /// Automatically download episode thumbnails, up to
    /// <see cref="MaxAutoThumbnails"/> per episode.
    /// </summary>
    public bool AutoDownloadThumbnails { get; set; } = true;

    /// <summary>
    /// The maximum number of thumbnails to download for each episode. Set to
    /// <c>0</c> to disable the limit.
    /// </summary>
    [Range(0, 30)]
    [Visibility(Size = DisplayElementSize.Small, DisableWhenMemberIsSet = nameof(AutoDownloadThumbnails), DisableWhenSetTo = false)]
    public int MaxAutoThumbnails { get; set; } = 1;

    /// <summary>
    /// Automatically download images of creators and characters, up to
    /// <see cref="MaxAutoStaffImages"/> per person.
    /// </summary>
    public bool AutoDownloadStaffImages { get; set; } = true;

    /// <summary>
    /// The maximum number of images to download for each creator or
    /// character. Set to <c>0</c> to disable the limit.
    /// </summary>
    [Range(0, 30)]
    [Visibility(Size = DisplayElementSize.Small, DisableWhenMemberIsSet = nameof(AutoDownloadStaffImages), DisableWhenSetTo = false)]
    public int MaxAutoStaffImages { get; set; } = 10;

    /// <summary>
    /// Automatically download studio images.
    /// </summary>
    public bool AutoDownloadStudioImages { get; set; } = true;

    #endregion

    #region Helpers

    /// <summary>
    /// Whether any image type is downloaded at all.
    /// </summary>
    [JsonIgnore]
    public bool AnyEnabled => AutoDownloadBackdrops || AutoDownloadPosters || AutoDownloadLogos || AutoDownloadBanners || AutoDownloadThumbnails ||
        AutoDownloadStaffImages || AutoDownloadStudioImages;

    /// <summary>
    /// Whether any image of one kind of entity is downloaded, which is when
    /// its images are worth asking for at all.
    /// </summary>
    /// <param name="entityType">The kind of entity.</param>
    /// <returns><c>true</c> when one of its image types is downloaded.</returns>
    public bool AnyEnabledFor(MetadataEntityType entityType)
    {
        if (entityType == MetadataEntityType.Creator || entityType == MetadataEntityType.Character)
            return AutoDownloadStaffImages;
        if (entityType == MetadataEntityType.Studio || entityType == MetadataEntityType.Network)
            return AutoDownloadStudioImages;
        if (entityType == MetadataEntityType.Episode)
            return AutoDownloadThumbnails;

        return AutoDownloadPosters || AutoDownloadLogos || AutoDownloadBackdrops || AutoDownloadBanners;
    }

    /// <summary>
    /// Whether one image type of one kind of entity is downloaded, and how
    /// many of it.
    /// </summary>
    /// <param name="entityType">The kind of entity the images are for.</param>
    /// <param name="imageType">The image type.</param>
    /// <returns>
    /// Whether it is downloaded and how many at most (0 for no limit), or
    /// <c>null</c> when no setting covers it, such as a disc.
    /// </returns>
    public (bool Enabled, int MaxCount)? GetRule(MetadataEntityType entityType, ImageEntityType imageType)
    {
        if (entityType == MetadataEntityType.Creator || entityType == MetadataEntityType.Character)
            return imageType is ImageEntityType.Primary ? (AutoDownloadStaffImages, MaxAutoStaffImages) : null;
        if (entityType == MetadataEntityType.Studio)
            return imageType is ImageEntityType.Primary ? (AutoDownloadStudioImages, 0) : null;
        if (entityType == MetadataEntityType.Network)
            return imageType is ImageEntityType.Primary or ImageEntityType.Logo ? (AutoDownloadStudioImages, 0) : null;
        if (entityType == MetadataEntityType.Episode)
            return imageType is ImageEntityType.Backdrop ? (AutoDownloadThumbnails, MaxAutoThumbnails) : null;

        return imageType switch
        {
            ImageEntityType.Primary => (AutoDownloadPosters, MaxAutoPosters),
            ImageEntityType.Logo => (AutoDownloadLogos, MaxAutoLogos),
            ImageEntityType.Backdrop => (AutoDownloadBackdrops, MaxAutoBackdrops),
            ImageEntityType.Banner => (AutoDownloadBanners, MaxAutoBanners),
            _ => null,
        };
    }

    #endregion
}
