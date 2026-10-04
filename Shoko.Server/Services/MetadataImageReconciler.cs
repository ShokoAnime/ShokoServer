using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Image.Exceptions;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Settings;

namespace Shoko.Server.Services;

/// <summary>
///   Links a plugin source's images to one of its entities and schedules the
///   ones to download, from the candidates its provider offered.
/// </summary>
/// <remarks>
///   Per downloaded image type, candidates are ordered by preferred language,
///   then the source's order. The entry's stored default, the image its own
///   source pinned, takes the first of the type's slots, whatever its
///   language, and the first others up to the limit take the rest; those
///   are marked desired, and links to images no longer offered removed. A
///   type the settings skip is left as it is; one no setting covers is
///   linked but never downloaded. A shared creator, character or studio is
///   reconciled one job at a time, in every language.
/// </remarks>
/// <param name="imageManager">Adds and links the images.</param>
/// <param name="entityLocks">Serialises the reconciling of an entity shared between entries.</param>
/// <param name="logger">Where refused images are reported.</param>
public class MetadataImageReconciler(IImageManager imageManager, MetadataEntryLocks entityLocks, ILogger<MetadataImageReconciler> logger)
{
    #region Constants

    /// <summary>
    ///   The longest resource ID the image table holds.
    /// </summary>
    public const int MaxResourceIDLength = 128;

    /// <summary>
    ///   The longest language or country code the image table holds.
    /// </summary>
    private const int MaxCodeLength = 5;

    #endregion

    #region Reconcile

    /// <summary>
    ///   Brings an entity's images from its source in line with what the
    ///   source offers, and schedules the desired downloads.
    /// </summary>
    /// <param name="entity">The entity, on <paramref name="source"/>.</param>
    /// <param name="source">The source the images and their links belong to.</param>
    /// <param name="candidates">Every image the source offers for the entity.</param>
    /// <param name="settings">The image settings in effect for the source.</param>
    /// <param name="originalLanguageCode">
    ///   The entry's original language, which the "main" entry in the
    ///   language order stands for, or <c>null</c> when unknown.
    /// </param>
    /// <param name="force">Whether to download the desired images again even when they are there.</param>
    /// <param name="entityLocked">
    ///   Whether the caller already holds the entity's entry lock, as an image
    ///   job for a shared entry does, so it is not taken again.
    /// </param>
    /// <param name="cancellationToken">Cancels the wait for the entity's lock.</param>
    /// <returns>How many images are linked from the source afterwards, over the types handled.</returns>
    /// <exception cref="ArgumentNullException">An argument is <c>null</c>.</exception>
    public async Task<int> Reconcile(
        IWithImages entity,
        MetadataSource source,
        IReadOnlyList<ImageCandidate> candidates,
        MetadataImageSettings settings,
        string? originalLanguageCode = null,
        bool force = false,
        bool entityLocked = false,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(settings);
        if (imageManager.GetTemplateUrlForSource(source) is null)
        {
            logger.LogWarning("Not linking images for {Entity}: {Source} has no template URL registered.", entity.ID, source);
            return 0;
        }

        var entityType = entity.ID.EntityType;
        var shared = IsShared(entityType);
        var languages = shared ? Array.Empty<TitleLanguage>() : GetLanguages(settings, originalLanguageCode);
        var valid = candidates.Where(candidate => IsValid(entity, candidate)).ToList();
        var linked = 0;
        using (shared && !entityLocked ? await entityLocks.Acquire(entity.ID, cancellationToken).ConfigureAwait(false) : null)
        {
            var types = valid.Select(candidate => candidate.ImageType)
                .Concat(imageManager.GetImageCrossReferencesForEntity(entity, Filter(source, null)).Select(xref => xref.ImageType))
                .Where(type => type is not ImageEntityType.None)
                .Distinct()
                .ToList();
            foreach (var imageType in types)
            {
                var rule = settings.GetRule(entityType, imageType);
                if (rule is { Enabled: false })
                    continue;

                var ofType = valid.Where(candidate => candidate.ImageType == imageType).ToList();
                var defaultID = source == entity.ID.Source ? (entity as IMetadataDefaultImageSource)?.GetDefaultResourceID(imageType) : null;
                linked += ReconcileType(entity, source, imageType, ofType, defaultID, rule?.MaxCount, languages);
            }
        }

        await imageManager.ScheduleAutoDownloadsForEntity(entity, imageSource: source, xrefSource: source, force: force).ConfigureAwait(false);
        return linked;
    }

    /// <summary>
    ///   Links one image type's candidates and removes the links to the ones
    ///   no longer offered.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <param name="source">The source.</param>
    /// <param name="imageType">The image type.</param>
    /// <param name="candidates">The type's candidates, in the source's order.</param>
    /// <param name="defaultID">The resource ID of the entry's stored default of the type, or <c>null</c>.</param>
    /// <param name="maxCount">
    ///   How many to download at most (0 for no limit), or <c>null</c> when
    ///   none are downloaded.
    /// </param>
    /// <param name="languages">The preferred languages, in order.</param>
    /// <returns>How many images of the type are linked afterwards.</returns>
    private int ReconcileType(
        IWithImages entity,
        MetadataSource source,
        ImageEntityType imageType,
        IReadOnlyList<ImageCandidate> candidates,
        string? defaultID,
        int? maxCount,
        IReadOnlyList<TitleLanguage> languages
    )
    {
        var indexed = candidates
            .Select((candidate, index) => (Candidate: candidate, Language: GetLanguage(candidate), Index: index))
            .DistinctBy(tuple => tuple.Candidate.ResourceID)
            .ToList();
        var desired = new HashSet<string>();
        if (maxCount is { } max)
        {
            // The stored default is the first within the limit, whatever its language.
            if (defaultID is not null && indexed.Any(tuple => tuple.Candidate.ResourceID == defaultID))
                desired.Add(defaultID);
            desired.UnionWith(indexed
                .Where(tuple => tuple.Candidate.ResourceID != defaultID && (languages.Count == 0 || languages.Contains(tuple.Language)))
                .OrderBy(tuple => IndexOf(languages, tuple.Language))
                .ThenBy(tuple => tuple.Index)
                .Take(max > 0 ? max - desired.Count : int.MaxValue)
                .Select(tuple => tuple.Candidate.ResourceID));
        }

        // The preferred languages first, in their order, then the rest, each in the source's order.
        var ordered = indexed
            .OrderByDescending(tuple => languages.Contains(tuple.Language))
            .ThenBy(tuple => IndexOf(languages, tuple.Language))
            .ThenBy(tuple => tuple.Index)
            .Select(tuple => tuple.Candidate)
            .ToList();
        // Only the first link to an image is kept; any more are duplicates.
        var groups = imageManager.GetImageCrossReferencesForEntity(entity, Filter(source, imageType))
            .GroupBy(xref => xref.ImageID)
            .ToList();
        foreach (var duplicate in groups.SelectMany(group => group.Skip(1)))
            imageManager.RemoveImageCrossReference(duplicate);
        var xrefs = groups.ToDictionary(group => group.Key, group => group.First());
        var kept = new HashSet<Guid>();
        var ordering = 0;
        foreach (var candidate in ordered)
        {
            var image = GetOrAddImage(source, candidate);
            if (image is null || !kept.Add(image.ID))
                continue;

            var isDesired = desired.Contains(candidate.ResourceID);
            var hasRating = candidate.Rating is >= 1 and <= 10 && candidate.RatingVotes is null or >= 0;
            if (!xrefs.TryGetValue(image.ID, out var xref))
            {
                var data = new ImageCrossReferenceData
                {
                    ImageType = imageType,
                    Source = source,
                    Ordering = ordering,
                    IsDesired = isDesired,
                    IsEnabled = true,
                };
                if (hasRating)
                {
                    data.Rating = candidate.Rating;
                    data.RatingVotes = candidate.RatingVotes ?? 0;
                }

                try
                {
                    imageManager.AddImageCrossReference(entity, image, data);
                }
                catch (ImageCrossReferenceExistsException ex)
                {
                    // Linked since it was read, so it is updated instead.
                    Update(ex.CrossReference, candidate, ordering, isDesired, hasRating);
                }
            }
            else
            {
                Update(xref, candidate, ordering, isDesired, hasRating);
            }

            ordering++;
        }

        foreach (var xref in xrefs.Values.Where(xref => !kept.Contains(xref.ImageID)))
            imageManager.RemoveImageCrossReference(xref);

        return kept.Count;
    }

    /// <summary>
    ///   Brings a link already there in line with a candidate.
    /// </summary>
    /// <param name="xref">The link.</param>
    /// <param name="candidate">The candidate it links.</param>
    /// <param name="ordering">Its place among the type's links.</param>
    /// <param name="isDesired">Whether it is to be downloaded.</param>
    /// <param name="hasRating">Whether the candidate's rating is usable.</param>
    private void Update(IImageCrossReference xref, ImageCandidate candidate, int ordering, bool isDesired, bool hasRating)
    {
        var data = new ImageCrossReferenceUpdateData
        {
            Ordering = ordering,
            IsDesired = isDesired,
        };
        if (hasRating)
        {
            data.Rating = candidate.Rating;
            data.RatingVotes = candidate.RatingVotes ?? 0;
        }

        imageManager.UpdateImageCrossReference(xref, data);
    }

    #endregion

    #region Helpers

    /// <summary>
    ///   Whether an entity of a kind is shared between entries, so more than
    ///   one image job may reach it.
    /// </summary>
    /// <param name="entityType">The kind.</param>
    /// <returns><c>true</c> for a creator, character, studio or network.</returns>
    private static bool IsShared(MetadataEntityType entityType)
        => entityType == MetadataEntityType.Creator || entityType == MetadataEntityType.Character || entityType == MetadataEntityType.Studio ||
            entityType == MetadataEntityType.Network;

    /// <summary>
    ///   The filter for the source's own links to the entity's images.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="imageType">One image type, or <c>null</c> for all.</param>
    /// <returns>The filter.</returns>
    private static Abstractions.Metadata.Image.Options.ImageCrossReferenceFilteringOptions Filter(MetadataSource source, ImageEntityType? imageType)
        => new() { ImageSource = source, XrefSource = source, ImageType = imageType, LinkedEntityImages = false };

    /// <summary>
    ///   Whether a candidate can be stored, logging why not.
    /// </summary>
    /// <param name="entity">The entity, for the log.</param>
    /// <param name="candidate">The candidate.</param>
    /// <returns><c>true</c> when it can.</returns>
    private bool IsValid(IWithImages entity, ImageCandidate candidate)
    {
        if (candidate.ImageType is ImageEntityType.None || string.IsNullOrWhiteSpace(candidate.ResourceID))
        {
            logger.LogDebug("Skipping an image for {Entity} with no type or resource ID.", entity.ID);
            return false;
        }

        if (candidate.ResourceID.Length > MaxResourceIDLength)
        {
            logger.LogWarning("Skipping an image for {Entity}: its resource ID is longer than {Length} characters.", entity.ID, MaxResourceIDLength);
            return false;
        }

        return true;
    }

    /// <summary>
    ///   The stored image for a candidate, adding it when it is new.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="candidate">The candidate.</param>
    /// <returns>The image, or <c>null</c> when it cannot be stored.</returns>
    private IImage? GetOrAddImage(MetadataSource source, ImageCandidate candidate)
    {
        if (imageManager.GetImageBySourceAndRemoteResourceID(source, candidate.ResourceID) is { } image)
            return image;

        try
        {
            var data = new ImageData
            {
                Source = source,
                ResourceID = candidate.ResourceID,
                LanguageCode = Code(candidate.LanguageCode),
                CountryCode = Code(candidate.CountryCode),
            };
            if (candidate is { Width: > 0, Height: > 0 })
            {
                data.Width = candidate.Width;
                data.Height = candidate.Height;
            }

            return imageManager.AddImage(data);
        }
        catch (Exception ex) when (ex is UnsupportedImageTypeException or ImageDataExistsException or MissingImageSourceTemplateUrlException)
        {
            logger.LogWarning(ex, "Skipping image {ResourceID} from {Source}.", candidate.ResourceID, source);
            return null;
        }
    }

    /// <summary>
    ///   A language or country code that fits the image table.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <returns>The code, or <c>null</c> when it is blank or too long.</returns>
    private static string? Code(string? code)
        => string.IsNullOrWhiteSpace(code) || code.Length > MaxCodeLength ? null : code;

    /// <summary>
    ///   The language of the text in a candidate, <see cref="TitleLanguage.None"/>
    ///   for none.
    /// </summary>
    /// <param name="candidate">The candidate.</param>
    /// <returns>The language.</returns>
    private static TitleLanguage GetLanguage(ImageCandidate candidate)
        => (candidate.LanguageCode ?? string.Empty).GetTitleLanguage();

    /// <summary>
    ///   Where a language sits in the preferred order, or after all of them.
    /// </summary>
    /// <param name="languages">The preferred languages.</param>
    /// <param name="language">The language.</param>
    /// <returns>The position.</returns>
    private static int IndexOf(IReadOnlyList<TitleLanguage> languages, TitleLanguage language)
    {
        for (var index = 0; index < languages.Count; index++)
            if (languages[index] == language)
                return index;
        return languages.Count;
    }

    /// <summary>
    ///   The preferred languages, with "main" standing for the entry's
    ///   original language.
    /// </summary>
    /// <param name="settings">The source's image settings.</param>
    /// <param name="originalLanguageCode">The entry's original language, or <c>null</c>.</param>
    /// <returns>The languages, in order.</returns>
    public static IReadOnlyList<TitleLanguage> GetLanguages(MetadataImageSettings settings, string? originalLanguageCode)
    {
        var main = string.IsNullOrWhiteSpace(originalLanguageCode) ? TitleLanguage.Unknown : originalLanguageCode.GetTitleLanguage();
        return settings.ImageLanguageOrder
            .Select(language => language is TitleLanguage.Main ? main is TitleLanguage.None or TitleLanguage.Unknown ? (TitleLanguage?)null : main : language)
            .OfType<TitleLanguage>()
            .Distinct()
            .ToList();
    }

    #endregion
}
