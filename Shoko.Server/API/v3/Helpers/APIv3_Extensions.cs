using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Image.Exceptions;
using Shoko.Abstractions.Metadata.Image.Options;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.ImageManagement;
using Shoko.Server.Extensions;
using Shoko.Server.Models.Shoko.Embedded;
using Shoko.Server.Server;

namespace Shoko.Server.API.v3.Helpers;

public static class APIv3_Extensions
{
    public static CreatorRoleType ToCreatorRole(this ICrew crew)
    {
        var (department, job) = TmdbCompatibility.DepartmentAndJob(crew);
        return ToCreatorRole(department, job);
    }

    private static CreatorRoleType ToCreatorRole(string department, string job)
        => department switch
        {
            // TODO: Implement this.
            _ => CreatorRoleType.Staff,
        };

    public static List<SeasonWithYear> ToV3Dto(this IEnumerable<(int Year, YearlySeason Season)> seasons)
        => seasons
            .Select(season => new SeasonWithYear(season.Year, season.Season))
            .ToList();

    public static ComponentVersion ToDto(this ReleaseVersionInformation componentVersion)
        => new()
        {
            Commit = componentVersion.SourceRevision,
            Description = componentVersion.Description,
            ReleaseChannel = componentVersion.Channel,
            ReleaseDate = componentVersion.ReleasedAt,
            Tag = componentVersion.ReleaseTag,
            Version = componentVersion.Version.ToSemanticVersioningString(),
        };

    public static ComponentVersion ToDto(this WebReleaseVersionInformation componentVersion)
        => new()
        {
            Commit = componentVersion.SourceRevision,
            Description = componentVersion.Description,
            ReleaseChannel = componentVersion.Channel,
            ReleaseDate = componentVersion.ReleasedAt,
            Tag = componentVersion.ReleaseTag,
            Version = componentVersion.Version.ToSemanticVersioningString(),
            MinimumServerVersion = componentVersion.MinimumServerVersion?.ToSemanticVersioningString(),
        };

    public static IEnumerable<IImage> InLanguage(this IEnumerable<IImage> imageList, IReadOnlySet<TitleLanguage>? language = null)
        => language != null && language.Count > 0
            ? imageList.Where(title => language.Contains(title.Language))
            : imageList;

    public static Images ToDto(
        this IEnumerable<IImage> imageList,
        IReadOnlySet<TitleLanguage>? language = null,
        IImage? preferredPoster = null,
        IImage? preferredBackdrop = null,
        IImage? preferredLogo = null,
        bool preferredImages = false,
        bool randomizeImages = false,
        bool showLinkedIDs = false,
        RemoteUrlInclusion includeRemoteUrl = RemoteUrlInclusion.False,
        Func<MetadataSource, string?>? remoteUrlTemplate = null)
    {
        var images = new Images();
        foreach (var image in imageList)
        {
            if (language != null && !language.Contains(image.Language))
                continue;

            bool? preferredOverride = null;
            switch (image.Type)
            {
                case ImageEntityType.Primary:
                    if (preferredPoster is not null)
                        preferredOverride = image.IsEnabled && preferredPoster.Equals(image);
                    images.Posters.Add(new(image, showLinkedIDs, preferredOverride, includeRemoteUrl, remoteUrlTemplate?.Invoke(image.Source)));
                    break;
                case ImageEntityType.Banner:
                    images.Banners.Add(new(image, showLinkedIDs, null, includeRemoteUrl, remoteUrlTemplate?.Invoke(image.Source)));
                    break;
                case ImageEntityType.Backdrop:
                    if (preferredBackdrop is not null)
                        preferredOverride = image.IsEnabled && preferredBackdrop.Equals(image);
                    images.Backdrops.Add(new(image, showLinkedIDs, preferredOverride, includeRemoteUrl, remoteUrlTemplate?.Invoke(image.Source)));
                    break;
                case ImageEntityType.Logo:
                    if (preferredLogo is not null)
                        preferredOverride = image.IsEnabled && preferredLogo.Equals(image);
                    images.Logos.Add(new(image, showLinkedIDs, preferredOverride, includeRemoteUrl, remoteUrlTemplate?.Invoke(image.Source)));
                    break;
                case ImageEntityType.Disc:
                    images.Discs.Add(new(image, showLinkedIDs, null, includeRemoteUrl, remoteUrlTemplate?.Invoke(image.Source)));
                    break;
            }
        }

        if (preferredImages)
        {
            SetPreferredOrDefaultImage(images.Posters, randomizeImages);
            SetPreferredOrDefaultImage(images.Backdrops, randomizeImages);
            SetPreferredOrDefaultImage(images.Banners, randomizeImages);
            SetPreferredOrDefaultImage(images.Logos, randomizeImages);
        }

        return images;
    }

    /// <summary>
    /// The links an image list of an entity, made with the same options, sees
    /// its images through.
    /// </summary>
    /// <param name="imageManager">The image manager.</param>
    /// <param name="entity">The entity the images are listed for.</param>
    /// <param name="options">The options the images were listed with, or <c>null</c> for all.</param>
    /// <returns>The links, the entity's own first for each image and type.</returns>
    public static IReadOnlyList<IImageCrossReference> GetCrossReferencesForImageList(this IImageManager imageManager, IWithImages entity, ImageFilteringOptions? options = null)
        => imageManager.GetImageCrossReferencesForEntity(entity, ToCrossReferenceOptions(options));

    /// <summary>
    /// The links an image list of a group as a user sees it, made with the
    /// same options, sees its images through.
    /// </summary>
    /// <param name="imageManager">The image manager.</param>
    /// <param name="view">The group as the user sees it.</param>
    /// <param name="options">The options the images were listed with, or <c>null</c> for all.</param>
    /// <returns>The links.</returns>
    public static IReadOnlyList<IImageCrossReference> GetCrossReferencesForImageList(this IImageManager imageManager, AnimeGroupView view, ImageFilteringOptions? options = null)
        => view.GetImageCrossReferences(imageManager, ToCrossReferenceOptions(options));

    /// <summary>
    /// Turns image list options into the matching link options.
    /// </summary>
    /// <param name="options">The image list options, or <c>null</c> for all.</param>
    /// <returns>The link options.</returns>
    private static ImageCrossReferenceFilteringOptions ToCrossReferenceOptions(ImageFilteringOptions? options)
        => new()
        {
            ImageSource = options?.ImageSource,
            ImageType = options?.ImageType,
            XrefSource = options?.XrefSource,
            IsEnabled = options?.IsEnabled,
            IsDesired = options?.IsDesired,
            IsPreferred = options?.IsPreferred,
            IsAvailable = options?.IsAvailable,
            IsPrimaryImage = options?.IsPrimaryImage,
            IsPrimaryAvailable = options?.IsPrimaryAvailable,
            LinkedEntityImages = options?.LinkedEntityImages,
        };

    /// <summary>
    /// Links an uploaded image to the entity it was uploaded for, as a user's
    /// own enabled image, and makes it the preferred one of its type when asked.
    /// </summary>
    /// <param name="imageManager">The image manager.</param>
    /// <param name="entity">The entity the image was uploaded for.</param>
    /// <param name="image">The uploaded image.</param>
    /// <param name="imageType">The type the image was uploaded as.</param>
    /// <param name="preferred">Whether to make it the preferred image of its type for the entity.</param>
    /// <returns>The stored image, its link to the entity, and whether the link was made now rather than found.</returns>
    /// <exception cref="ArgumentException">The image is not stored.</exception>
    public static (IImage Image, IImageCrossReference CrossReference, bool Created) LinkUploadedImage(
        this IImageManager imageManager,
        IWithImages entity,
        IImage image,
        ImageEntityType imageType,
        bool preferred
    )
    {
        IImageCrossReference xref;
        var created = true;
        try
        {
            xref = imageManager.AddImageCrossReference(entity, image, new()
            {
                ImageType = imageType,
                IsEnabled = true,
                IsDesired = true,
                Source = MetadataSource.User,
            });
        }
        catch (ImageCrossReferenceExistsException ex)
        {
            image = ex.Image;
            xref = ex.CrossReference;
            created = false;
        }

        if (preferred)
            xref = imageManager.SetPreferredImageForEntity(xref);

        return (image, xref, created);
    }

    /// <summary>
    /// Enables or disables an image for an entity, on every link the entity
    /// sees the image through for the type, its own and its linked entries'.
    /// </summary>
    /// <param name="imageManager">The image manager.</param>
    /// <param name="entity">The entity the image is shown for.</param>
    /// <param name="imageType">The type the image is shown as.</param>
    /// <param name="image">The image.</param>
    /// <param name="enabled">Whether the image should be enabled.</param>
    /// <returns>The links, updated, the entity's own first; empty when the entity does not see the image as that type.</returns>
    public static IReadOnlyList<IImageCrossReference> SetImageEnabledForEntity(
        this IImageManager imageManager,
        IWithImages entity,
        ImageEntityType imageType,
        IImage image,
        bool enabled
    )
        => [
            .. imageManager.GetImageCrossReferencesForEntity(entity, new() { ImageType = imageType })
                .Where(xref => xref.ImageID == image.ID)
                .ToList()
                .Select(xref => xref.IsEnabled == enabled ? xref : imageManager.UpdateImageCrossReference(xref, new() { IsEnabled = enabled })),
        ];

    /// <summary>
    /// Adds to each image the links it is seen through, matched by image and type.
    /// </summary>
    /// <param name="images">The images.</param>
    /// <param name="crossReferences">The links of the entity the images are listed for.</param>
    /// <returns>The same images.</returns>
    public static Images WithCrossReferences(this Images images, IEnumerable<IImageCrossReference> crossReferences)
    {
        AttachCrossReferences([.. images.Posters, .. images.Backdrops, .. images.Banners, .. images.Logos, .. images.Discs], crossReferences);
        return images;
    }

    /// <summary>
    /// Adds to each image on the page the links it is seen through, matched by image and type.
    /// </summary>
    /// <param name="images">The page of images.</param>
    /// <param name="crossReferences">The links of the entity the images are listed for.</param>
    /// <returns>The same page.</returns>
    public static ListResult<Image> WithCrossReferences(this ListResult<Image> images, IEnumerable<IImageCrossReference> crossReferences)
    {
        AttachCrossReferences(images.List, crossReferences);
        return images;
    }

    /// <summary>
    /// Adds to the image the links it is seen through, matched by image and type.
    /// </summary>
    /// <param name="image">The image.</param>
    /// <param name="crossReferences">The links of the entity the image is shown for.</param>
    /// <returns>The same image.</returns>
    public static Image WithCrossReferences(this Image image, IEnumerable<IImageCrossReference> crossReferences)
    {
        AttachCrossReferences([image], crossReferences);
        return image;
    }

    private static void AttachCrossReferences(IEnumerable<Image> images, IEnumerable<IImageCrossReference> crossReferences)
    {
        var lookup = crossReferences.ToLookup(xref => (xref.ImageID, xref.ImageType));
        foreach (var image in images)
            image.CrossReferences = [.. lookup[(image.UID, image.Type)].Select(xref => new ImageCrossReferenceSlim(xref))];
    }

    private static void SetPreferredOrDefaultImage(List<Image> images, bool randomizeImages = false)
    {
        var image = randomizeImages
            ? images.GetRandomElement()
            : images.FirstOrDefault(i => i.Preferred) ?? images.FirstOrDefault();
        images.Clear();
        if (image is not null)
            images.Add(image);
    }

    public static IReadOnlyList<Title> ToTitleDto<TTitle>(this IEnumerable<TTitle> titles, string? mainTitle = null, ITitle? preferredTitle = null, IReadOnlySet<TitleLanguage>? language = null) where TTitle : ITitle
    {
        if (language != null && language.Count > 0)
            titles = titles.WhereInLanguages(language);

        return titles
            .Select(title => new Title(title, mainTitle, preferredTitle))
            .OrderByDescending(title => title.Preferred)
            .ThenByDescending(title => title.Default)
            .ThenBy(title => title.Language)
            .ToList();
    }

    public static IReadOnlyList<Overview> ToOverviewDto<TText>(this IEnumerable<TText> overviews, string? mainOverview = null, IText? preferredOverview = null, IReadOnlySet<TitleLanguage>? language = null) where TText : IText
    {
        if (language != null && language.Count > 0)
            overviews = overviews.WhereInLanguages(language);

        return overviews
            .Select(overview => new Overview(overview, mainOverview, preferredOverview))
            .OrderByDescending(overview => overview.Preferred)
            .ThenByDescending(overview => overview.Default)
            .ThenBy(overview => overview.Language)
            .ToList();
    }

    public static IReadOnlyList<ContentRating> ToDto(this IEnumerable<IContentRating> contentRatings, IReadOnlySet<TitleLanguage>? language = null)
    {
        if (language != null && language.Count > 0)
            contentRatings = contentRatings.WhereInLanguages(language);

        return contentRatings
            .Select(contentRating => new ContentRating(contentRating))
            .ToList();
    }
}
