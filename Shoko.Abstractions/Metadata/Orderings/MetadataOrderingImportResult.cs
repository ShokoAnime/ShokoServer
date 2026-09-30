using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Orderings;

/// <summary>
///   What an ordering import did, or why it could not read the payload.
/// </summary>
public sealed record MetadataOrderingImportResult
{
    /// <summary>
    ///   Why the payload could not be read. When there are any, nothing was
    ///   written.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    ///   Whether the payload was read.
    /// </summary>
    public bool Succeeded => Errors.Count is 0;

    /// <summary>
    ///   Whether this was a dry run, which wrote nothing.
    /// </summary>
    public bool DryRun { get; init; }

    /// <summary>
    ///   What was done with each ordering of the payload, in its order.
    /// </summary>
    public IReadOnlyList<MetadataOrderingImportEntry> Orderings { get; init; } = [];

    /// <summary>
    ///   How many orderings were made.
    /// </summary>
    public int CreatedCount => Orderings.Count(entry => entry.Outcome is MetadataOrderingImportOutcome.Created);

    /// <summary>
    ///   How many orderings were replaced.
    /// </summary>
    public int ReplacedCount => Orderings.Count(entry => entry.Outcome is MetadataOrderingImportOutcome.Replaced);

    /// <summary>
    ///   How many orderings were skipped.
    /// </summary>
    public int SkippedCount => Orderings.Count(entry => entry.Outcome is MetadataOrderingImportOutcome.Skipped);

    /// <summary>
    ///   How many images were restored from the payload.
    /// </summary>
    public int ImagesFromPayload => CountImages(MetadataOrderingImageImportStatus.FromPayload);

    /// <summary>
    ///   How many images were restored from their remote source.
    /// </summary>
    public int ImagesFromUrl => CountImages(MetadataOrderingImageImportStatus.FromUrl);

    /// <summary>
    ///   How many images wait on a queued download.
    /// </summary>
    public int ImagesPending => CountImages(MetadataOrderingImageImportStatus.Pending);

    /// <summary>
    ///   How many images could not be restored.
    /// </summary>
    public int ImagesFailed => CountImages(MetadataOrderingImageImportStatus.Failed);

    /// <summary>
    ///   How many images of every ordering have a status.
    /// </summary>
    /// <param name="status">The status.</param>
    /// <returns>The count.</returns>
    private int CountImages(MetadataOrderingImageImportStatus status)
        => Orderings.Sum(entry => entry.Images.Count(image => image.Status == status));
}

/// <summary>
///   What an ordering import did with one ordering of the payload.
/// </summary>
public sealed record MetadataOrderingImportEntry
{
    /// <summary>
    ///   The ordering's place in the payload, from 0.
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    ///   The ordering's name in the payload.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///   The name it was stored under, which differs from <see cref="Name"/>
    ///   when both were kept, or <c>null</c> when it was skipped.
    /// </summary>
    public string? StoredName { get; init; }

    /// <summary>
    ///   The AniDB anime the ordering is for.
    /// </summary>
    public required int AnidbAnimeID { get; init; }

    /// <summary>
    ///   The Shoko series it was imported into, or <c>null</c> when the anime
    ///   is not in the collection.
    /// </summary>
    public MetadataGuid? SeriesID { get; init; }

    /// <summary>
    ///   The local ordering made or replaced, or <c>null</c> when it was
    ///   skipped or on a dry run that would make a new one.
    /// </summary>
    public MetadataGuid? OrderingID { get; init; }

    /// <summary>
    ///   What was done.
    /// </summary>
    public required MetadataOrderingImportOutcome Outcome { get; init; }

    /// <summary>
    ///   Why it was skipped, or <c>null</c>.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    ///   Whether it was chosen as its series' ordering.
    /// </summary>
    public bool IsPreferred { get; init; }

    /// <summary>
    ///   The episodes dropped from their groups because they could not be
    ///   found in the series.
    /// </summary>
    public IReadOnlyList<MetadataOrderingUnresolvedEpisode> UnresolvedEpisodes { get; init; } = [];

    /// <summary>
    ///   What was done with each image of the ordering and its groups.
    /// </summary>
    public IReadOnlyList<MetadataOrderingImageImportEntry> Images { get; init; } = [];

    /// <summary>
    ///   What else was changed on the way, such as a second special group
    ///   made a regular one.
    /// </summary>
    public IReadOnlyList<string> Notes { get; init; } = [];
}

/// <summary>
///   An episode an ordering import could not find in the series.
/// </summary>
/// <param name="GroupIndex">The group's place in the ordering, from 0.</param>
/// <param name="GroupName">The group's name.</param>
/// <param name="AnidbEpisodeID">The AniDB episode the payload names, if any.</param>
/// <param name="EpisodeType">The episode's type in the payload, if given.</param>
/// <param name="EpisodeNumber">The episode's number in the payload, if given.</param>
/// <param name="Reason">Why it could not be found.</param>
public sealed record MetadataOrderingUnresolvedEpisode(
    int GroupIndex,
    string GroupName,
    int? AnidbEpisodeID,
    EpisodeType? EpisodeType,
    int? EpisodeNumber,
    string Reason
);

/// <summary>
///   What an ordering import did with one image.
/// </summary>
public sealed record MetadataOrderingImageImportEntry
{
    /// <summary>
    ///   The place of the group the image is for, from 0, or <c>null</c> for
    ///   the ordering itself.
    /// </summary>
    public int? GroupIndex { get; init; }

    /// <summary>
    ///   What the image is for the entry, such as its poster.
    /// </summary>
    public required ImageEntityType ImageType { get; init; }

    /// <summary>
    ///   What was done.
    /// </summary>
    public required MetadataOrderingImageImportStatus Status { get; init; }

    /// <summary>
    ///   The image on this server, when it was restored or linked.
    /// </summary>
    public Guid? ImageID { get; init; }

    /// <summary>
    ///   Why it failed or was not taken from the first place tried, or
    ///   <c>null</c>.
    /// </summary>
    public string? Reason { get; init; }
}
