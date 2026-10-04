using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.Scheduling;

/// <summary>
///   Chooses the priority a job is queued with from what it is for, for the
///   job types whose pool only holds their own jobs: a tier, plus
///   <see cref="NewOffset"/> for something new to the server and
///   <see cref="PrioritizedOffset"/> for a prioritized job.
/// </summary>
internal static class JobPriorities
{
    #region Constants

    /// <summary>
    ///   What a prioritized job adds to its tier.
    /// </summary>
    public const int PrioritizedOffset = 50;

    /// <summary>
    ///   What a job for a file, an entry or an image new to the server adds to
    ///   its tier, so bulk work on known ones never holds back new ones.
    /// </summary>
    public const int NewOffset = 50;

    /// <summary>
    ///   One mebibyte, 1024 × 1024 bytes.
    /// </summary>
    private const long MiB = 1024L * 1024L;

    /// <summary>
    ///   One gibibyte, 1024 × 1024 × 1024 bytes.
    /// </summary>
    private const long GiB = 1024L * MiB;

    #endregion

    #region File Size

    /// <summary>
    ///   The priority of a hashing or media info job for a file, smaller files
    ///   first: 40 under 200 MiB, 30 under 1 GiB, 20 under 1.5 GiB, 10 under
    ///   4 GiB and 0 from there, the boundaries in powers of 1024, and 20 for an
    ///   unknown size.
    /// </summary>
    /// <param name="fileSize">The file's size in bytes, or <c>null</c> or <c>0</c> when unknown.</param>
    /// <param name="isNew">Whether the file is new to the server, rather than re-hashed, re-read or repaired.</param>
    /// <param name="prioritize">Whether the job is prioritized.</param>
    /// <returns>The priority.</returns>
    public static int ForFileSize(long? fileSize, bool isNew, bool prioritize)
    {
        var tier = fileSize switch
        {
            null or <= 0 => 20,
            < 200 * MiB => 40,
            < GiB => 30,
            < GiB + GiB / 2 => 20,
            < 4 * GiB => 10,
            _ => 0,
        };
        return Compose(tier, isNew, prioritize);
    }

    #endregion

    #region Metadata Entries

    /// <summary>
    ///   The priority of an image job for a metadata entry: 30 for series,
    ///   movies and collections, 20 for seasons and episodes, 0 for creators,
    ///   characters, studios, networks and channels, and 10 for anything else.
    /// </summary>
    /// <param name="entityType">The entry's kind.</param>
    /// <param name="isNew">Whether this is the entry's first image run, after it was just linked or created.</param>
    /// <param name="prioritize">Whether the job is prioritized.</param>
    /// <returns>The priority.</returns>
    public static int ForMetadataEntry(MetadataEntityType entityType, bool isNew, bool prioritize)
    {
        var tier = IsMainEntry(entityType) ? 30
            : IsSubEntry(entityType) ? 20
            : IsPersonOrCompany(entityType) ? 0
            : 10;
        return Compose(tier, isNew, prioritize);
    }

    #endregion

    #region Images

    /// <summary>
    ///   The priority of an image download: 30 for the posters, backdrops,
    ///   banners and logos of series, movies and collections, 20 for season and
    ///   episode images, 0 for people and company images, and 10 for anything
    ///   else, an image of an unknown owner included.
    /// </summary>
    /// <param name="entityType">The kind of entry the image belongs to, or <c>null</c> when unknown.</param>
    /// <param name="imageType">The image's type.</param>
    /// <param name="isNew">Whether the image's file has never been downloaded, rather than re-downloaded or repaired.</param>
    /// <param name="prioritize">Whether the job is prioritized.</param>
    /// <returns>The priority.</returns>
    public static int ForImage(MetadataEntityType? entityType, ImageEntityType imageType, bool isNew, bool prioritize)
    {
        var tier = entityType switch
        {
            { } kind when IsMainEntry(kind) => IsArtwork(imageType) ? 30 : 10,
            { } kind when IsSubEntry(kind) => 20,
            { } kind when IsPersonOrCompany(kind) => 0,
            _ => 10,
        };
        return Compose(tier, isNew, prioritize);
    }

    #endregion

    #region Classification

    /// <summary>
    ///   Adds the boosts for something new and for a prioritized job to a tier.
    /// </summary>
    /// <param name="tier">The tier.</param>
    /// <param name="isNew">Whether the job is for something new to the server.</param>
    /// <param name="prioritize">Whether the job is prioritized.</param>
    /// <returns>The priority.</returns>
    private static int Compose(int tier, bool isNew, bool prioritize)
        => tier + (isNew ? NewOffset : 0) + (prioritize ? PrioritizedOffset : 0);

    /// <summary>
    ///   Whether an image type is a poster, a backdrop, a banner or a logo.
    /// </summary>
    /// <param name="imageType">The image type.</param>
    /// <returns><c>true</c> when it is.</returns>
    private static bool IsArtwork(ImageEntityType imageType)
        => imageType is ImageEntityType.Primary or ImageEntityType.Backdrop or ImageEntityType.Banner or ImageEntityType.Logo;

    /// <summary>
    ///   Whether a kind is a series, a movie or a collection.
    /// </summary>
    /// <param name="entityType">The kind.</param>
    /// <returns><c>true</c> when it is.</returns>
    private static bool IsMainEntry(MetadataEntityType entityType)
        => entityType == MetadataEntityType.Series || entityType == MetadataEntityType.Movie || entityType == MetadataEntityType.Collection;

    /// <summary>
    ///   Whether a kind is a season or an episode.
    /// </summary>
    /// <param name="entityType">The kind.</param>
    /// <returns><c>true</c> when it is.</returns>
    private static bool IsSubEntry(MetadataEntityType entityType)
        => entityType == MetadataEntityType.Season || entityType == MetadataEntityType.Episode;

    /// <summary>
    ///   Whether a kind is a creator, a character, a studio, a network or a
    ///   channel.
    /// </summary>
    /// <param name="entityType">The kind.</param>
    /// <returns><c>true</c> when it is.</returns>
    private static bool IsPersonOrCompany(MetadataEntityType entityType)
        => entityType == MetadataEntityType.Creator
            || entityType == MetadataEntityType.Character
            || entityType == MetadataEntityType.Studio
            || entityType == MetadataEntityType.Network
            || entityType == MetadataEntityType.Channel;

    #endregion
}
