using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Image.Options;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Abstractions.User;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models.Shoko.Embedded;

/// <summary>
///   A group as one user sees it: only the series the user may see, and the
///   name, overview and images taken from those. A group is visible to a user
///   when it holds at least one series, at any level, the user may see.
/// </summary>
public sealed class AnimeGroupView
{
    #region Fields

    private readonly HashSet<int>? _visibleSeriesIDs;

    private AnimeSeries? _mainSeries;

    private bool _mainSeriesResolved;

    private string? _name;

    private string? _description;

    private IReadOnlyList<AnimeSeries>? _series;

    private IReadOnlyList<AnimeGroup>? _children;

    private IReadOnlyList<string>? _ancestorNames;

    private bool? _hasPartialAncestor;

    #endregion

    #region Constructors

    private AnimeGroupView(AnimeGroup group, IUser? user, IReadOnlyList<AnimeSeries> allSeries, HashSet<int>? visibleSeriesIDs)
    {
        Group = group;
        User = user;
        AllSeries = allSeries;
        _visibleSeriesIDs = visibleSeriesIDs;
    }

    /// <summary>
    ///   Makes the view of a group for a user.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="user">The user, or <c>null</c> to see everything.</param>
    /// <returns>The view.</returns>
    public static AnimeGroupView For(AnimeGroup group, IUser? user)
    {
        var allSeries = group.AllSeries;
        if (IsUnrestricted(user))
            return new(group, user, allSeries, null);

        var visible = allSeries.Where(series => user!.IsAllowedToSee(series)).ToList();
        if (visible.Count == allSeries.Count)
            return new(group, user, allSeries, null);

        return new(group, user, visible, visible.Select(series => series.AnimeSeriesID).ToHashSet());
    }

    /// <summary>
    ///   Whether the user is known to see every series, without asking for
    ///   each one.
    /// </summary>
    /// <param name="user">The user, or <c>null</c> for no user.</param>
    /// <returns><c>true</c> when there is no user or it has no restricted tags.</returns>
    public static bool IsUnrestricted(IUser? user)
        => user is null || (user is JMMUser jmmUser && !jmmUser.HasRestrictions());

    #endregion

    #region Properties

    /// <summary>
    ///   The group.
    /// </summary>
    public AnimeGroup Group { get; }

    /// <summary>
    ///   The user the group is seen by, or <c>null</c> for none.
    /// </summary>
    public IUser? User { get; }

    /// <summary>
    ///   The user's ID, or 0 when there is no user.
    /// </summary>
    public int UserID => User?.LocalID ?? 0;

    /// <summary>
    ///   Whether the user may see every series in the group, so the view is
    ///   the group as everyone sees it.
    /// </summary>
    public bool IsComplete => _visibleSeriesIDs is null;

    /// <summary>
    ///   Whether the user may see the group: it holds at least one series the
    ///   user may see, or the user sees everything.
    /// </summary>
    public bool IsVisible => AllSeries.Count > 0 || IsUnrestricted(User);

    /// <summary>
    ///   The series, at any level, the user may see, in the group's order.
    /// </summary>
    public IReadOnlyList<AnimeSeries> AllSeries { get; }

    /// <summary>
    ///   The series directly in the group the user may see.
    /// </summary>
    public IReadOnlyList<AnimeSeries> Series => _series ??= IsComplete
        ? Group.Series
        : Group.Series.Where(IsVisibleSeries).ToList();

    /// <summary>
    ///   The direct sub-groups the user may see.
    /// </summary>
    public IReadOnlyList<AnimeGroup> Children => _children ??= IsComplete
        ? Group.Children
        : Group.Children.Where(child => child.AllSeries.Any(IsVisibleSeries)).ToList();

    /// <summary>
    ///   The sub-groups, at any level, the user may see.
    /// </summary>
    public IEnumerable<AnimeGroup> AllChildren => IsComplete
        ? Group.AllChildren
        : Group.AllChildren.Where(child => child.AllSeries.Any(IsVisibleSeries));

    /// <summary>
    ///   The group's main series if the user may see it, else the first series
    ///   the user may see.
    /// </summary>
    public AnimeSeries? MainSeries
    {
        get
        {
            if (_mainSeriesResolved)
                return _mainSeries;

            var main = Group.MainSeries;
            _mainSeries = IsComplete || (main is not null && IsVisibleSeries(main)) ? main : AllSeries.FirstOrDefault();
            _mainSeriesResolved = true;
            return _mainSeries;
        }
    }

    /// <summary>
    ///   The ID of the preferred series a user set for the group, if the user
    ///   may see it.
    /// </summary>
    public int? PreferredSeriesID => Group.DefaultAnimeSeriesID is { } seriesID && (IsComplete || _visibleSeriesIDs!.Contains(seriesID))
        ? seriesID
        : null;

    /// <summary>
    ///   Whether the view shows the same main series as the group itself.
    /// </summary>
    public bool HasGroupMainSeries => IsComplete || MainSeries?.AnimeSeriesID == Group.MainSeries?.AnimeSeriesID;

    /// <summary>
    ///   The group's name: the one a user gave it, else the title of the main
    ///   series of the view.
    /// </summary>
    public string Name => _name ??= HasGroupMainSeries
        ? Group.GroupName
        : Group.CustomTitle is { Value: { } custom } && !string.IsNullOrWhiteSpace(custom)
            ? custom
            : MainSeries?.Title ?? string.Empty;

    /// <summary>
    ///   The sort name made from <see cref="Name"/>.
    /// </summary>
    public string SortName => AnimeGroup.ToGroupSortName(Name);

    /// <summary>
    ///   The group's overview: the one a user gave it, else the preferred
    ///   overview of the main series of the view.
    /// </summary>
    public string Description => _description ??= HasGroupMainSeries
        ? Group.Description
        : Group.CustomOverview?.Value ?? MainSeries?.PreferredOverview?.Value ?? string.Empty;

    /// <summary>
    ///   What the view's images come from: the group when the user sees all of
    ///   it, else the main series of the view, so no hidden series' images show.
    ///   A custom-named group adds its own uploads, see <see cref="ShowsGroupUploads"/>.
    /// </summary>
    public IWithImages? ImageEntity => IsComplete ? Group : MainSeries;

    /// <summary>
    ///   Whether a group the user sees only part of still shows the images a
    ///   user uploaded to it, as it has a custom name of its own.
    /// </summary>
    public bool ShowsGroupUploads => !IsComplete && Group.CustomTitle is { Value: { } title } && !string.IsNullOrWhiteSpace(title);

    /// <summary>
    ///   The names of the groups above this one, as the user sees them.
    /// </summary>
    public IReadOnlyList<string> AncestorNames => _ancestorNames ??= IsUnrestricted(User)
        ? Group.AllGroupsAbove.Select(group => group.GroupName).ToList()
        : Group.AllGroupsAbove.Select(group => For(group, User).Name).ToList();

    /// <summary>
    ///   Whether a group above this one hides a series from the user, so its
    ///   name may differ for the user.
    /// </summary>
    public bool HasPartialAncestor => _hasPartialAncestor ??= !IsUnrestricted(User) &&
        Group.AllGroupsAbove.Any(group => !For(group, User).IsComplete);

    /// <summary>
    ///   Whether anything the filters read of the group differs for the user
    ///   from what everyone sees.
    /// </summary>
    public bool IsPersonal => !IsComplete || HasPartialAncestor;

    #endregion

    #region Images

    /// <summary>
    ///   Whether an image is one a user uploaded to a group: a local image
    ///   linked to nothing but groups, so it is no series' or provider's image.
    /// </summary>
    /// <param name="imageSource">The image's source.</param>
    /// <param name="imageID">The image's ID.</param>
    /// <returns><c>true</c> if the image is a group's own upload.</returns>
    public static bool IsGroupUpload(MetadataSource imageSource, Guid imageID)
        => imageSource.IsLocal && RepoFactory.ShokoImage_Entity.GetByImageID(imageID)
            .All(xref => xref.EntitySource == MetadataSource.Shoko && xref.EntityType == MetadataEntityType.Collection);

    /// <summary>
    ///   The group's own links to its uploads the view shows, none unless
    ///   <see cref="ShowsGroupUploads"/>.
    /// </summary>
    /// <param name="imageManager">The image manager.</param>
    /// <param name="options">The filtering options, if any.</param>
    /// <returns>The links.</returns>
    public IReadOnlyList<IImageCrossReference> GetGroupUploadCrossReferences(IImageManager imageManager, ImageCrossReferenceFilteringOptions? options = null)
        => ShowsGroupUploads
            ? imageManager.GetImageCrossReferencesForEntity(Group, OwnOnly(options)).Where(xref => IsGroupUpload(xref.ImageSource, xref.ImageID)).ToList()
            : [];

    /// <summary>
    ///   The images of the view: the group's when the user sees all of it,
    ///   else its shown uploads and the main series' images.
    /// </summary>
    /// <param name="imageManager">The image manager.</param>
    /// <param name="options">The filtering options, if any.</param>
    /// <returns>The images.</returns>
    public IReadOnlyList<IImage> GetImages(IImageManager imageManager, ImageFilteringOptions? options = null)
    {
        if (IsComplete)
            return imageManager.GetImagesForEntity(Group, options);

        var uploads = ShowsGroupUploads
            ? imageManager.GetImagesForEntity(Group, OwnOnly(options)).Where(image => IsGroupUpload(image.Source, image.ID))
            : [];
        var series = MainSeries is { } mainSeries ? imageManager.GetImagesForEntity(mainSeries, options) : [];
        return [.. uploads.Concat(series).DistinctBy(image => (image.ID, image.Type))];
    }

    /// <summary>
    ///   The links the images of <see cref="GetImages"/> are seen through.
    /// </summary>
    /// <param name="imageManager">The image manager.</param>
    /// <param name="options">The filtering options, if any.</param>
    /// <returns>The links.</returns>
    public IReadOnlyList<IImageCrossReference> GetImageCrossReferences(IImageManager imageManager, ImageCrossReferenceFilteringOptions? options = null)
    {
        if (IsComplete)
            return imageManager.GetImageCrossReferencesForEntity(Group, options);

        var series = MainSeries is { } mainSeries ? imageManager.GetImageCrossReferencesForEntity(mainSeries, options) : [];
        return [.. GetGroupUploadCrossReferences(imageManager, options).Concat(series)];
    }

    /// <summary>
    ///   The preferred image of a type the view shows: a preferred upload
    ///   first, then the main series' preferred one.
    /// </summary>
    /// <param name="imageManager">The image manager.</param>
    /// <param name="imageType">The image type.</param>
    /// <returns>The image, or <c>null</c> when none is preferred.</returns>
    public IImage? GetPreferredImageForType(IImageManager imageManager, ImageEntityType imageType)
        => GetImages(imageManager, new() { ImageType = imageType, IsPreferred = true }).FirstOrDefault();

    /// <summary>
    ///   The best image of each type the view shows. A preferred upload wins,
    ///   then the main series' best image, then the first enabled upload.
    /// </summary>
    /// <param name="imageManager">The image manager.</param>
    /// <returns>The images, one per type.</returns>
    public IEnumerable<IImage> GetBestImages(IImageManager imageManager)
    {
        if (IsComplete)
            return ((IWithImages)Group).GetBestImages();

        if (!ShowsGroupUploads)
            return (MainSeries as IWithImages)?.GetBestImages() ?? [];

        var images = new List<IImage>();
        foreach (var imageType in Enum.GetValues<ImageEntityType>().Except([ImageEntityType.None]))
        {
            var uploads = GetGroupUploadCrossReferences(imageManager, new() { ImageType = imageType, IsEnabled = true });
            var preferred = uploads.FirstOrDefault(xref => xref is { IsPreferred: true, IsPrimaryAvailable: true });
            var fallback = uploads.FirstOrDefault(xref => xref.IsPrimaryAvailable) ?? uploads.FirstOrDefault();
            var image = (preferred is null ? null : Wrap(preferred))
                ?? (MainSeries as IWithImages)?.GetBestImageForType(imageType)
                ?? (fallback is null ? null : Wrap(fallback));
            if (image is not null)
                images.Add(image);
        }

        return images;
    }

    /// <summary>
    ///   Wraps the primary image of one of the group's own links.
    /// </summary>
    /// <param name="xref">The link.</param>
    /// <returns>The image, or <c>null</c> when it is gone.</returns>
    private static IImage? Wrap(IImageCrossReference xref)
        => xref.GetPrimaryImage() is { } image ? ImageStub.Wrap(image, xref, false) : null;

    /// <summary>
    ///   Copies image filtering options, limited to the group's own links.
    /// </summary>
    /// <param name="options">The options, if any.</param>
    /// <returns>The copy.</returns>
    private static ImageFilteringOptions OwnOnly(ImageFilteringOptions? options)
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
            AsPrimaryImage = options?.AsPrimaryImage ?? false,
            IsPrimaryAvailable = options?.IsPrimaryAvailable,
            LinkedEntityImages = false,
        };

    /// <summary>
    ///   Copies cross-reference filtering options, limited to the group's own
    ///   links.
    /// </summary>
    /// <param name="options">The options, if any.</param>
    /// <returns>The copy.</returns>
    private static ImageCrossReferenceFilteringOptions OwnOnly(ImageCrossReferenceFilteringOptions? options)
        => new()
        {
            ImageSource = options?.ImageSource,
            ImageType = options?.ImageType,
            XrefSource = options?.XrefSource,
            EntitySource = options?.EntitySource,
            EntityType = options?.EntityType,
            IsEnabled = options?.IsEnabled,
            IsDesired = options?.IsDesired,
            IsPreferred = options?.IsPreferred,
            IsAvailable = options?.IsAvailable,
            IsPrimaryImage = options?.IsPrimaryImage,
            IsPrimaryAvailable = options?.IsPrimaryAvailable,
            LinkedEntityImages = false,
        };

    #endregion

    #region Methods

    /// <summary>
    ///   Whether the user may see a series of the group.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns><c>true</c> if the user may see it.</returns>
    public bool IsVisibleSeries(AnimeSeries series)
        => _visibleSeriesIDs?.Contains(series.AnimeSeriesID) ?? true;

    #endregion
}
