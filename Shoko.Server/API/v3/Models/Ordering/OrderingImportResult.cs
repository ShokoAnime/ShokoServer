using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Orderings;

namespace Shoko.Server.API.v3.Models.Ordering;

/// <summary>
/// What an ordering import did, or on a dry run would do.
/// </summary>
public class OrderingImportResult
{
    /// <summary>
    /// Whether this was a dry run, which wrote nothing.
    /// </summary>
    [Required]
    public bool DryRun { get; init; }

    /// <summary>
    /// How many orderings were made.
    /// </summary>
    [Required]
    public int Created { get; init; }

    /// <summary>
    /// How many orderings were replaced.
    /// </summary>
    [Required]
    public int Replaced { get; init; }

    /// <summary>
    /// How many orderings were skipped.
    /// </summary>
    [Required]
    public int Skipped { get; init; }

    /// <summary>
    /// How many images were restored from the payload.
    /// </summary>
    [Required]
    public int ImagesFromPayload { get; init; }

    /// <summary>
    /// How many images were restored from their remote source.
    /// </summary>
    [Required]
    public int ImagesFromUrl { get; init; }

    /// <summary>
    /// How many images wait on a queued download.
    /// </summary>
    [Required]
    public int ImagesPending { get; init; }

    /// <summary>
    /// How many images could not be restored.
    /// </summary>
    [Required]
    public int ImagesFailed { get; init; }

    /// <summary>
    /// What was done with each ordering of the payload, in its order.
    /// </summary>
    [Required]
    public IReadOnlyList<OrderingImportEntry> Orderings { get; init; }

    /// <summary>
    /// Builds the model of an import's result.
    /// </summary>
    /// <param name="result">The result.</param>
    public OrderingImportResult(MetadataOrderingImportResult result)
    {
        DryRun = result.DryRun;
        Created = result.CreatedCount;
        Replaced = result.ReplacedCount;
        Skipped = result.SkippedCount;
        ImagesFromPayload = result.ImagesFromPayload;
        ImagesFromUrl = result.ImagesFromUrl;
        ImagesPending = result.ImagesPending;
        ImagesFailed = result.ImagesFailed;
        Orderings = [.. result.Orderings.Select(entry => new OrderingImportEntry(entry))];
    }

    /// <summary>
    /// What was done with one ordering of the payload.
    /// </summary>
    public class OrderingImportEntry
    {
        /// <summary>
        /// The ordering's place in the payload, from 0.
        /// </summary>
        [Required]
        public int Index { get; init; }

        /// <summary>
        /// The ordering's name in the payload.
        /// </summary>
        [Required]
        public string Name { get; init; }

        /// <summary>
        /// The name it was stored under, when it was not skipped.
        /// </summary>
        public string? StoredName { get; init; }

        /// <summary>
        /// The AniDB anime the ordering is for.
        /// </summary>
        [Required]
        public int AnidbAnimeID { get; init; }

        /// <summary>
        /// The Shoko series it was imported into, when the anime is in the
        /// collection.
        /// </summary>
        public int? ShokoSeriesID { get; init; }

        /// <summary>
        /// The full ID of the local ordering made or replaced.
        /// </summary>
        public string? OrderingID { get; init; }

        /// <summary>
        /// What was done.
        /// </summary>
        [Required, JsonConverter(typeof(StringEnumConverter))]
        public MetadataOrderingImportOutcome Outcome { get; init; }

        /// <summary>
        /// Why it was skipped.
        /// </summary>
        public string? Reason { get; init; }

        /// <summary>
        /// Whether it was chosen as its series' ordering.
        /// </summary>
        [Required]
        public bool IsPreferred { get; init; }

        /// <summary>
        /// The episodes dropped from their groups because they could not be
        /// found in the series.
        /// </summary>
        [Required]
        public IReadOnlyList<UnresolvedEpisode> UnresolvedEpisodes { get; init; }

        /// <summary>
        /// The full IDs of the networks the ordering was linked to, or on a
        /// dry run would be, in order.
        /// </summary>
        [Required]
        public IReadOnlyList<string> Networks { get; init; }

        /// <summary>
        /// The full IDs of the networks this server did not have, kept as
        /// stubs with an empty name until their source saves them.
        /// </summary>
        [Required]
        public IReadOnlyList<string> StubbedNetworks { get; init; }

        /// <summary>
        /// What was done with each image of the ordering and its groups.
        /// </summary>
        [Required]
        public IReadOnlyList<ImageImportEntry> Images { get; init; }

        /// <summary>
        /// What else was changed on the way.
        /// </summary>
        [Required]
        public IReadOnlyList<string> Notes { get; init; }

        /// <summary>
        /// Builds the model of one ordering's result.
        /// </summary>
        /// <param name="entry">The result.</param>
        public OrderingImportEntry(MetadataOrderingImportEntry entry)
        {
            Index = entry.Index;
            Name = entry.Name;
            StoredName = entry.StoredName;
            AnidbAnimeID = entry.AnidbAnimeID;
            ShokoSeriesID = entry.SeriesID is { } seriesID && seriesID.Source == MetadataSource.Shoko && seriesID.TryGetNumericID<int>(out var localID) ? localID : null;
            OrderingID = entry.OrderingID?.ToString();
            Outcome = entry.Outcome;
            Reason = entry.Reason;
            IsPreferred = entry.IsPreferred;
            UnresolvedEpisodes = [.. entry.UnresolvedEpisodes.Select(episode => new UnresolvedEpisode(episode))];
            Networks = [.. entry.Networks.Select(network => network.ToString())];
            StubbedNetworks = [.. entry.StubbedNetworks.Select(network => network.ToString())];
            Images = [.. entry.Images.Select(image => new ImageImportEntry(image))];
            Notes = entry.Notes;
        }
    }

    /// <summary>
    /// An episode the import could not find in the series.
    /// </summary>
    public class UnresolvedEpisode
    {
        /// <summary>
        /// The group's place in the ordering, from 0.
        /// </summary>
        [Required]
        public int GroupIndex { get; init; }

        /// <summary>
        /// The group's name.
        /// </summary>
        [Required]
        public string GroupName { get; init; }

        /// <summary>
        /// The AniDB episode the payload names.
        /// </summary>
        public int? AnidbEpisodeID { get; init; }

        /// <summary>
        /// The episode's type in the payload.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public EpisodeType? EpisodeType { get; init; }

        /// <summary>
        /// The episode's number in the payload.
        /// </summary>
        public int? EpisodeNumber { get; init; }

        /// <summary>
        /// Why it could not be found.
        /// </summary>
        [Required]
        public string Reason { get; init; }

        /// <summary>
        /// Builds the model of an episode that could not be found.
        /// </summary>
        /// <param name="episode">The episode.</param>
        public UnresolvedEpisode(MetadataOrderingUnresolvedEpisode episode)
        {
            GroupIndex = episode.GroupIndex;
            GroupName = episode.GroupName;
            AnidbEpisodeID = episode.AnidbEpisodeID;
            EpisodeType = episode.EpisodeType;
            EpisodeNumber = episode.EpisodeNumber;
            Reason = episode.Reason;
        }
    }

    /// <summary>
    /// What was done with one image.
    /// </summary>
    public class ImageImportEntry
    {
        /// <summary>
        /// The place of the group the image is for, from 0, or <c>null</c>
        /// for the ordering itself.
        /// </summary>
        public int? GroupIndex { get; init; }

        /// <summary>
        /// What the image is for the entry.
        /// </summary>
        [Required, JsonConverter(typeof(StringEnumConverter))]
        public ImageEntityType ImageType { get; init; }

        /// <summary>
        /// What was done.
        /// </summary>
        [Required, JsonConverter(typeof(StringEnumConverter))]
        public MetadataOrderingImageImportStatus Status { get; init; }

        /// <summary>
        /// The image on this server, when it was restored or linked.
        /// </summary>
        public Guid? ImageID { get; init; }

        /// <summary>
        /// Why it failed or was not taken from the first place tried.
        /// </summary>
        public string? Reason { get; init; }

        /// <summary>
        /// Builds the model of one image's result.
        /// </summary>
        /// <param name="image">The result.</param>
        public ImageImportEntry(MetadataOrderingImageImportEntry image)
        {
            GroupIndex = image.GroupIndex;
            ImageType = image.ImageType;
            Status = image.Status;
            ImageID = image.ImageID;
            Reason = image.Reason;
        }
    }
}
