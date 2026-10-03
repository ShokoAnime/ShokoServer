using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.Exceptions;
using Shoko.Abstractions.Metadata.Orderings;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Server.Services.Ordering;
using Shoko.Server.Services.OrderingTransfer;

namespace Shoko.Server.Services;

/// <summary>
///   Writes orderings into an export file and reads one back into local
///   orderings, translating the series and episodes to and from AniDB IDs
///   and moving the images through the image manager.
/// </summary>
/// <param name="orderingService">Reads the orderings to write and keeps the ones read.</param>
/// <param name="metadataService">Finds the series and episodes, and the Shoko entries for AniDB IDs.</param>
/// <param name="imageManager">Reads, adds, links and downloads the images.</param>
/// <param name="imageFiles">Keeps an imported file as the held copy of a remote image.</param>
/// <param name="studioStore">Tells which networks an import would add as stubs.</param>
/// <param name="systemService">Tells the server's version for the file.</param>
/// <param name="logger">Where an import is reported.</param>
public class MetadataOrderingTransferService(
    IMetadataOrderingService orderingService,
    IMetadataService metadataService,
    IImageManager imageManager,
    IImageFileStore imageFiles,
    IMetadataStudioStore studioStore,
    ISystemService systemService,
    ILogger<MetadataOrderingTransferService> logger
) : IMetadataOrderingTransferService
{
    /// <summary>
    ///   The name an ordering without one is imported under.
    /// </summary>
    internal const string UnnamedOrdering = "Imported Ordering";

    #region Export

    /// <inheritdoc />
    public async Task<MetadataOrderingExportResult> Export(
        Stream destination,
        MetadataOrderingExportOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite)
            throw new ArgumentException("The stream cannot be written.", nameof(destination));

        options ??= new();
        var container = ContainerFor(options);
        var context = new ExportContext(options, container);
        var document = new OrderingDocument
        {
            Format = OrderingDocument.FormatName,
            Version = OrderingDocument.CurrentVersion,
            ExportedAt = DateTime.UtcNow,
            Server = new() { Version = ServerVersion() },
        };

        // A zip gets each image file as it is read and its manifest last, so
        // no file is held in memory longer than it takes to copy it.
        if (container is MetadataOrderingContainer.Zip)
        {
            var archive = await OrderingDocumentSerializer.CreateArchive(destination, cancellationToken).ConfigureAwait(false);
            await using (archive.ConfigureAwait(false))
            {
                context.Archive = archive;
                await ExportOrderings(document, context, cancellationToken).ConfigureAwait(false);
                await OrderingDocumentSerializer.WriteManifest(archive, document, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            await ExportOrderings(document, context, cancellationToken).ConfigureAwait(false);
            await OrderingDocumentSerializer.WriteJson(destination, document, cancellationToken).ConfigureAwait(false);
        }

        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
        var images = document.Orderings.SelectMany(ordering => ordering.Images.Concat(ordering.Groups.SelectMany(group => group.Images))).ToList();
        return new()
        {
            Container = container,
            OrderingCount = document.Orderings.Count,
            ImageCount = images.Count,
            EmbeddedImageCount = images.Count(image => image.File is not null || image.Data is not null),
            Warnings = context.Warnings,
        };
    }

    /// <summary>
    ///   The file an export writes: the one the options name, or a zip when
    ///   they embed image files and plain JSON when they do not.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <returns><see cref="MetadataOrderingContainer.Json"/> or <see cref="MetadataOrderingContainer.Zip"/>.</returns>
    internal static MetadataOrderingContainer ContainerFor(MetadataOrderingExportOptions options)
        => options.Container is not MetadataOrderingContainer.Auto ? options.Container
            : options.ImageMode is MetadataOrderingImageExportMode.EmbedAll or MetadataOrderingImageExportMode.EmbedMissingRemote ? MetadataOrderingContainer.Zip
            : MetadataOrderingContainer.Json;

    /// <summary>
    ///   Writes the orderings the options select into the document.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="context">The export.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task that completes once every ordering is written.</returns>
    private async Task ExportOrderings(OrderingDocument document, ExportContext context, CancellationToken cancellationToken)
    {
        foreach (var ordering in SelectOrderings(context.Options, context.Warnings))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await ExportOrdering(ordering, context, cancellationToken).ConfigureAwait(false) is { } entry)
                document.Orderings.Add(entry);
        }
    }

    /// <summary>
    ///   What one export is writing.
    /// </summary>
    /// <param name="Options">The options.</param>
    /// <param name="Container">The file being written.</param>
    private sealed record ExportContext(MetadataOrderingExportOptions Options, MetadataOrderingContainer Container)
    {
        /// <summary>
        ///   The zip archive the image files go into, when writing one.
        /// </summary>
        public ZipArchive? Archive { get; set; }

        /// <summary>
        ///   The paths of the image files already in the archive.
        /// </summary>
        public HashSet<string> WrittenFiles { get; } = new(StringComparer.Ordinal);

        /// <summary>
        ///   What was left out and why.
        /// </summary>
        public List<string> Warnings { get; } = [];
    }

    /// <summary>
    ///   The server's version, for the file.
    /// </summary>
    /// <returns>The version, or <c>null</c> when it cannot be told.</returns>
    private string? ServerVersion()
        => systemService.Version?.Version?.ToSemanticVersioningString();

    /// <summary>
    ///   The orderings an export writes: the ones named and the ones of the
    ///   series named, or every local ordering when neither is.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="warnings">Gets the orderings and series that were not found.</param>
    /// <returns>The orderings, each once.</returns>
    private List<IOrdering> SelectOrderings(MetadataOrderingExportOptions options, List<string> warnings)
    {
        if (options.OrderingIDs is not { Count: > 0 } && options.SeriesIDs is not { Count: > 0 })
            return [.. orderingService.GetStoredOrderings(MetadataSource.User)];

        var orderings = new List<IOrdering>();
        foreach (var orderingID in options.OrderingIDs ?? [])
        {
            if (orderingID is not null && orderingService.GetOrdering(orderingID) is { } ordering)
                orderings.Add(ordering);
            else
                warnings.Add($"The ordering \"{orderingID}\" was not found.");
        }

        foreach (var seriesID in options.SeriesIDs ?? [])
        {
            if (seriesID is null || metadataService.GetSeries(seriesID) is not { } series)
            {
                warnings.Add($"The series \"{seriesID}\" was not found.");
                continue;
            }

            orderings.AddRange(orderingService.GetOrderings(series)
                .Where(ordering => !ordering.IsDefault && (ordering.ID.Source == MetadataSource.User || options.IncludeGlobalOrderings)));
        }

        return [.. orderings.DistinctBy(ordering => ordering.ID)];
    }

    /// <summary>
    ///   Writes one ordering, with its series and episodes as AniDB IDs.
    /// </summary>
    /// <param name="ordering">The ordering.</param>
    /// <param name="context">The export.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The ordering as written, or <c>null</c> when its series has no AniDB anime.</returns>
    private async Task<OrderingDocumentOrdering?> ExportOrdering(IOrdering ordering, ExportContext context, CancellationToken cancellationToken)
    {
        var series = ordering.Series ?? metadataService.GetSeries(ordering.SeriesID);
        if (series is null)
        {
            context.Warnings.Add($"The ordering \"{ordering.ID}\" was left out: its series \"{ordering.SeriesID}\" is not available.");
            return null;
        }

        // A placed special is written where it airs too, so it is placed again on import.
        var placement = (ordering as IPlacedOrdering)?.Placement;
        var byID = ordering.Episodes.DistinctBy(episode => episode.ID).ToDictionary(episode => episode.ID);
        var groups = new List<(ISeason Season, List<AnidbPlace> Episodes)>();
        foreach (var season in ordering.Seasons)
        {
            var episodes = new List<AnidbPlace>();
            var listed = placement is null
                ? season.Episodes
                : [.. placement.Listed(season.ID).Select(place => byID.GetValueOrDefault(place.EpisodeID)).OfType<IEpisode>()];
            foreach (var episode in listed)
            {
                var places = ToAnidb(episode);
                if (places.Count is 0)
                    context.Warnings.Add($"The episode \"{episode.ID}\" of the ordering \"{ordering.ID}\" was left out: it has no AniDB episode.");
                episodes.AddRange(places);
            }

            groups.Add((season, episodes));
        }

        if (AnidbAnimeOf(series, groups.SelectMany(group => group.Episodes)) is not { } animeID)
        {
            context.Warnings.Add($"The ordering \"{ordering.ID}\" was left out: its series \"{series.ID}\" has no AniDB anime.");
            return null;
        }

        var entry = new OrderingDocumentOrdering
        {
            Series = new() { AnidbAnimeId = animeID, Title = metadataService.GetShokoSeriesByAnidbID(animeID)?.Title ?? series.Title },
            Origin = ordering.ID.ToString(),
            Name = ordering.Name,
            Description = string.IsNullOrEmpty(ordering.Overview) ? null : ordering.Overview,
            Type = ordering.Type,
            IsPreferred = context.Options.IncludePreferred ? ordering.IsPreferred : null,
            Networks = [.. ordering.Networks.Select(network => network.ID.ToString())],
            Images = await ExportImages(ordering, context, cancellationToken).ConfigureAwait(false),
        };
        foreach (var (season, episodes) in groups)
        {
            entry.Groups.Add(new()
            {
                Name = season.Title,
                Description = season.DefaultOverview?.Value is { Length: > 0 } description ? description : null,
                IsSpecial = season.IsSpecial,
                Episodes = [.. episodes.Select(place => new OrderingDocumentEpisode
                {
                    AnidbAnimeId = place.AnimeID is > 0 ? place.AnimeID : null,
                    AnidbEpisodeId = place.EpisodeID,
                    Type = place.Type,
                    Number = place.Number,
                })],
                Images = await ExportImages(season, context, cancellationToken).ConfigureAwait(false),
            });
        }

        return entry;
    }

    /// <summary>
    ///   An episode as AniDB knows it.
    /// </summary>
    /// <param name="EpisodeID">The AniDB episode.</param>
    /// <param name="Type">Its type.</param>
    /// <param name="Number">Its number among the anime's episodes of its type.</param>
    /// <param name="AnimeID">Its AniDB anime, when known.</param>
    internal sealed record AnidbPlace(int EpisodeID, EpisodeType Type, int Number, int? AnimeID);

    /// <summary>
    ///   The AniDB episodes an episode of any source stands for: its own for a
    ///   Shoko or AniDB episode, and those of the Shoko episodes linked to it
    ///   for any other.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <returns>The AniDB episodes, in order; none when it has none.</returns>
    internal static IReadOnlyList<AnidbPlace> ToAnidb(IEpisode episode)
    {
        if (episode is IShokoEpisode shokoEpisode)
            return [FromShoko(shokoEpisode)];
        if (episode.ID.Source == MetadataSource.AniDB && episode.ID.TryGetNumericID<int>(out var episodeID))
            return [new(episodeID, episode.Type, episode.EpisodeNumber, episode.SeriesID.TryGetNumericID<int>(out var animeID) ? animeID : null)];

        return [.. episode.ShokoEpisodes.Select(FromShoko).DistinctBy(place => place.EpisodeID)];

        static AnidbPlace FromShoko(IShokoEpisode episode)
            => new(episode.AnidbEpisodeID, episode.Type, episode.EpisodeNumber, episode.Series?.AnidbAnimeID);
    }

    /// <summary>
    ///   The AniDB anime an ordering's series stands for: its own for a Shoko
    ///   or AniDB series, else the one most of its episodes are of, else the
    ///   first Shoko series linked to it.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <param name="episodes">The ordering's episodes as AniDB knows them.</param>
    /// <returns>The anime, or <c>null</c> when there is none.</returns>
    internal static int? AnidbAnimeOf(ISeries series, IEnumerable<AnidbPlace> episodes)
    {
        if (series is IShokoSeries shokoSeries)
            return shokoSeries.AnidbAnimeID;
        if (series.ID.Source == MetadataSource.AniDB && series.ID.TryGetNumericID<int>(out var animeID))
            return animeID;

        var mostUsed = episodes
            .Where(place => place.AnimeID is > 0)
            .GroupBy(place => place.AnimeID!.Value)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => (int?)group.Key)
            .FirstOrDefault();
        return mostUsed ?? series.ShokoSeries.FirstOrDefault()?.AnidbAnimeID;
    }

    /// <summary>
    ///   Writes the images linked to an ordering or a group, as the options
    ///   ask.
    /// </summary>
    /// <param name="entity">The ordering or group.</param>
    /// <param name="context">The export.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The images as written.</returns>
    private async Task<List<OrderingDocumentImage>> ExportImages(IWithImages entity, ExportContext context, CancellationToken cancellationToken)
    {
        var mode = context.Options.ImageMode;
        if (mode is MetadataOrderingImageExportMode.None)
            return [];

        var images = new List<OrderingDocumentImage>();
        var links = imageManager.GetImageCrossReferencesForEntity(entity, new() { LinkedEntityImages = false })
            .Where(link => link.IsEnabled && link.ImageType is not ImageEntityType.None);
        foreach (var link in links)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (imageManager.GetImageByID(link.ImageID) is not { } image)
                continue;

            var template = image.Source.IsLocal ? null : imageManager.GetTemplateUrlForSource(image.Source);
            var url = string.IsNullOrEmpty(template) ? null : string.Format(template, image.ResourceID);
            var embed = mode is MetadataOrderingImageExportMode.EmbedAll || (mode is MetadataOrderingImageExportMode.EmbedMissingRemote && url is null);
            if (!embed && url is null)
            {
                context.Warnings.Add($"The {link.ImageType} image \"{image.ID}\" of \"{entity.ID}\" was left out: it has no remote URL.");
                continue;
            }

            var file = embed ? await EmbedImage(image, url is not null, context, cancellationToken).ConfigureAwait(false) : null;
            if (embed && file is null)
            {
                if (url is null)
                {
                    context.Warnings.Add($"The {link.ImageType} image \"{image.ID}\" of \"{entity.ID}\" was left out: its file is not held and it has no remote URL.");
                    continue;
                }

                context.Warnings.Add($"The {link.ImageType} image \"{image.ID}\" of \"{entity.ID}\" is written by URL only: its file is not held.");
            }

            images.Add(new()
            {
                ImageType = link.ImageType,
                IsPreferred = link.IsPreferred,
                Language = string.IsNullOrEmpty(image.LanguageCode) ? null : image.LanguageCode,
                Width = image.Width,
                Height = image.Height,
                Source = url is null ? null : image.Source.Value,
                ResourceId = url is null ? null : image.ResourceID,
                Url = url,
                ContentType = string.IsNullOrEmpty(image.ContentType) ? null : image.ContentType,
                Sha256 = file?.Sha256,
                File = file?.Path,
                Data = file?.Data,
            });
        }

        return images;
    }

    /// <summary>
    ///   An image file an export carries: its hash, and its path in the zip
    ///   archive or its bytes inline as base64.
    /// </summary>
    /// <param name="Sha256">The file's SHA-256, in lowercase hex.</param>
    /// <param name="Path">Its path in the archive, when writing one.</param>
    /// <param name="Data">The file as base64, when writing plain JSON.</param>
    private sealed record EmbeddedFile(string Sha256, string? Path, string? Data);

    /// <summary>
    ///   Embeds an image's file: copied into the zip archive once per file,
    ///   after hashing it, or read whole for plain JSON, which holds it
    ///   inline anyway.
    /// </summary>
    /// <param name="image">The image.</param>
    /// <param name="mayDownload">Whether it can be downloaded from its source when not held.</param>
    /// <param name="context">The export.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The embedded file, or <c>null</c> when it is not held.</returns>
    private async Task<EmbeddedFile?> EmbedImage(IImage image, bool mayDownload, ExportContext context, CancellationToken cancellationToken)
    {
        if (await HeldImage(image, mayDownload).ConfigureAwait(false) is not { } held)
            return null;

        if (context.Archive is null)
            return ReadHeldImage(held) is { } bytes ? new(Sha256(bytes), null, Convert.ToBase64String(bytes)) : null;

        // Hash the file first, as its hash names its entry, then copy it in
        // unless an earlier image already wrote the same file.
        if (await HashHeldImage(held, cancellationToken).ConfigureAwait(false) is not { } sha256)
            return null;

        var path = OrderingDocumentSerializer.ImagePath(sha256, held.ContentType);
        if (context.WrittenFiles.Contains(path))
            return new(sha256, path, null);

        if (OpenHeldImage(held) is not { } stream)
            return null;

        await using (stream.ConfigureAwait(false))
            await OrderingDocumentSerializer.WriteImage(context.Archive, path, stream, cancellationToken).ConfigureAwait(false);
        context.WrittenFiles.Add(path);
        return new(sha256, path, null);
    }

    /// <summary>
    ///   An image whose file this server holds, downloading it first when it
    ///   is not held and may be.
    /// </summary>
    /// <param name="image">The image.</param>
    /// <param name="mayDownload">Whether it can be downloaded from its source.</param>
    /// <returns>The image, or <c>null</c> when its file is not held.</returns>
    private async Task<IImage?> HeldImage(IImage image, bool mayDownload)
    {
        if (image.IsAvailable || !mayDownload)
            return image;

        if (OpenHeldImage(image) is { } file)
        {
            await file.DisposeAsync().ConfigureAwait(false);
            return image;
        }

        try
        {
            if (!await imageManager.DownloadImage(image).ConfigureAwait(false))
                return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to download the image {ImageID} for an ordering export.", image.ID);
            return null;
        }

        return imageManager.GetImageByID(image.ID) ?? image;
    }

    /// <summary>
    ///   Opens an image's file as held on this server.
    /// </summary>
    /// <param name="image">The image.</param>
    /// <returns>The file, or <c>null</c> when it is not held or cannot be opened.</returns>
    private Stream? OpenHeldImage(IImage image)
    {
        try
        {
            return image.GetStream();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Unable to read the image {ImageID} for an ordering export.", image.ID);
            return null;
        }
    }

    /// <summary>
    ///   Reads an image's file as held on this server.
    /// </summary>
    /// <param name="image">The image.</param>
    /// <returns>The file, or <c>null</c> when it is not held, is empty or cannot be read.</returns>
    private byte[]? ReadHeldImage(IImage image)
    {
        try
        {
            using var stream = image.GetStream();
            if (stream is null)
                return null;

            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.Length is 0 ? null : buffer.ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Unable to read the image {ImageID} for an ordering export.", image.ID);
            return null;
        }
    }

    /// <summary>
    ///   Hashes an image's file as held on this server, reading it through
    ///   once.
    /// </summary>
    /// <param name="image">The image.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The file's SHA-256 in lowercase hex, or <c>null</c> when it is not held, is empty or cannot be read.</returns>
    private async Task<string?> HashHeldImage(IImage image, CancellationToken cancellationToken)
    {
        if (OpenHeldImage(image) is not { } stream)
            return null;

        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            await using (stream.ConfigureAwait(false))
            {
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var length = 0L;
                int read;
                while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    hash.AppendData(buffer, 0, read);
                    length += read;
                }

                return length is 0 ? null : Convert.ToHexStringLower(hash.GetHashAndReset());
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Unable to read the image {ImageID} for an ordering export.", image.ID);
            return null;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    ///   The SHA-256 of a file, in lowercase hex.
    /// </summary>
    /// <param name="bytes">The file.</param>
    /// <returns>The hash.</returns>
    internal static string Sha256(byte[] bytes)
        => Convert.ToHexStringLower(SHA256.HashData(bytes));

    #endregion

    #region Import

    /// <inheritdoc />
    public async Task<MetadataOrderingImportResult> Import(
        Stream source,
        MetadataOrderingImportOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        options ??= new();

        OrderingPayload payload;
        try
        {
            payload = await OrderingDocumentSerializer.Read(source, cancellationToken).ConfigureAwait(false);
        }
        catch (OrderingDocumentException ex)
        {
            return new() { Errors = [ex.Message], DryRun = options.DryRun };
        }

        using (payload)
        {
            var context = new ImportContext(options, payload);
            var entries = new List<MetadataOrderingImportEntry>();
            for (var index = 0; index < payload.Document.Orderings.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                entries.Add(await ImportOrdering(index, payload.Document.Orderings[index], context, cancellationToken).ConfigureAwait(false));
            }

            var result = new MetadataOrderingImportResult { DryRun = options.DryRun, Orderings = entries };
            logger.LogInformation(
                "Imported orderings{DryRun}: {Created} created, {Replaced} replaced, {Skipped} skipped; images {FromPayload} from the payload, {FromUrl} from their source, {Pending} pending, {Failed} failed.",
                options.DryRun ? " (dry run)" : string.Empty,
                result.CreatedCount,
                result.ReplacedCount,
                result.SkippedCount,
                result.ImagesFromPayload,
                result.ImagesFromUrl,
                result.ImagesPending,
                result.ImagesFailed
            );
            return result;
        }
    }

    /// <summary>
    ///   What one import is reading.
    /// </summary>
    /// <param name="Options">The options.</param>
    /// <param name="Payload">The payload.</param>
    private sealed record ImportContext(MetadataOrderingImportOptions Options, OrderingPayload Payload)
    {
        /// <summary>
        ///   The names this import gave local orderings, by series, so a dry
        ///   run sees its own orderings as a real one would.
        /// </summary>
        public Dictionary<MetadataGuid, List<string>> Named { get; } = [];
    }

    /// <summary>
    ///   Reads one ordering of the payload into a local ordering.
    /// </summary>
    /// <param name="index">Its place in the payload.</param>
    /// <param name="ordering">The ordering.</param>
    /// <param name="context">The import.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>What was done.</returns>
    private async Task<MetadataOrderingImportEntry> ImportOrdering(int index, OrderingDocumentOrdering ordering, ImportContext context, CancellationToken cancellationToken)
    {
        var options = context.Options;
        var notes = new List<string>();
        var name = string.IsNullOrWhiteSpace(ordering.Name) ? UnnamedOrdering : ordering.Name.Trim();
        if (string.IsNullOrWhiteSpace(ordering.Name))
            notes.Add($"The ordering has no name, so it is named \"{UnnamedOrdering}\".");

        var animeID = ordering.Series?.AnidbAnimeId ?? 0;
        var entry = new MetadataOrderingImportEntry { Index = index, Name = name, AnidbAnimeID = animeID, Outcome = MetadataOrderingImportOutcome.Skipped };
        if (animeID <= 0)
            return entry with { Reason = "It names no AniDB anime.", Notes = notes };
        if (metadataService.GetShokoSeriesByAnidbID(animeID) is not { } series)
            return entry with { Reason = $"The AniDB anime {animeID} is not in the collection.", Notes = notes };

        entry = entry with { SeriesID = series.ID };
        var (groups, unresolved) = ResolveGroups(ordering, series, notes);
        var networks = ResolveNetworks(ordering, notes);
        entry = entry with
        {
            UnresolvedEpisodes = unresolved,
            Networks = networks ?? [],
            StubbedNetworks = [.. (networks ?? []).Where(network => studioStore.GetNetwork(network) is null)],
        };

        // A local ordering of the series by the same name, or one this import named so.
        var existing = orderingService.GetOrderings(series).Where(stored => stored.ID.Source == MetadataSource.User).ToList();
        var named = context.Named.TryGetValue(series.ID, out var list) ? list : context.Named[series.ID] = [];
        var match = existing.FirstOrDefault(stored => SameName(stored.Name, name));
        var replace = false;
        var storedName = name;
        if (match is not null || named.Any(taken => SameName(taken, name)))
        {
            switch (options.ConflictMode)
            {
                case MetadataOrderingConflictMode.Replace:
                    replace = true;
                    break;
                case MetadataOrderingConflictMode.KeepBoth:
                    storedName = FreeName(name, [.. existing.Select(stored => stored.Name), .. named]);
                    match = null;
                    break;
                default:
                    return entry with { Reason = $"The series already has a local ordering named \"{name}\".", Notes = notes };
            }
        }

        var data = new MetadataLocalOrderingData
        {
            SeriesID = series.ID,
            Name = storedName,
            Overview = string.IsNullOrWhiteSpace(ordering.Description) ? null : ordering.Description,
            Networks = networks,
            Groups = groups,
        };
        var outcome = replace ? MetadataOrderingImportOutcome.Replaced : MetadataOrderingImportOutcome.Created;
        var preferred = options.ApplyPreferred && ordering.IsPreferred is true;
        if (options.DryRun)
        {
            if (!replace)
                named.Add(storedName);
            return entry with
            {
                Outcome = outcome,
                StoredName = storedName,
                OrderingID = match?.ID,
                IsPreferred = preferred,
                Images = [.. PlanImages(ordering, context)],
                Notes = notes,
            };
        }

        IOrdering? stored;
        try
        {
            if (replace && match is not null)
            {
                stored = orderingService.UpdateLocalOrdering(match.ID, data);
                if (stored is not null)
                    RemoveOwnImageLinks(stored);
            }
            else
            {
                stored = orderingService.CreateLocalOrdering(data);
            }
        }
        catch (ArgumentException ex)
        {
            return entry with { Reason = ex.Message, Notes = notes };
        }

        if (stored is null)
            return entry with { Reason = "The local ordering to replace is gone.", Notes = notes };
        if (!replace || match is null)
            named.Add(storedName);

        var images = new List<MetadataOrderingImageImportEntry>();
        images.AddRange(await RestoreImages(stored, null, ordering.Images, context, cancellationToken).ConfigureAwait(false));
        var seasons = stored.Seasons;
        for (var groupIndex = 0; groupIndex < ordering.Groups.Count && groupIndex < seasons.Count; groupIndex++)
            images.AddRange(await RestoreImages(seasons[groupIndex], groupIndex, ordering.Groups[groupIndex].Images, context, cancellationToken).ConfigureAwait(false));

        if (preferred)
        {
            try
            {
                orderingService.SetPreferredOrdering(series.ID, stored.ID);
            }
            catch (ArgumentException ex)
            {
                preferred = false;
                notes.Add($"It could not be chosen for its series: {ex.Message}");
            }
        }

        return entry with
        {
            Outcome = outcome,
            StoredName = storedName,
            OrderingID = stored.ID,
            IsPreferred = preferred,
            Images = images,
            Notes = notes,
        };
    }

    /// <summary>
    ///   Finds the Shoko episodes of an ordering's groups in the series,
    ///   dropping the ones it cannot find, and keeps at most one special group.
    /// </summary>
    /// <param name="ordering">The ordering.</param>
    /// <param name="series">The series.</param>
    /// <param name="notes">Gets what else was changed.</param>
    /// <returns>The groups to store, and the episodes that were dropped.</returns>
    internal (IReadOnlyList<MetadataLocalOrderingGroupData> Groups, IReadOnlyList<MetadataOrderingUnresolvedEpisode> Unresolved) ResolveGroups(
        OrderingDocumentOrdering ordering,
        IShokoSeries series,
        List<string> notes
    )
    {
        var episodes = series.Episodes;
        var byAnidbID = episodes
            .GroupBy(episode => episode.AnidbEpisodeID)
            .ToDictionary(group => group.Key, group => group.First());
        var byNumber = episodes
            .GroupBy(episode => (episode.Type, episode.EpisodeNumber))
            .ToDictionary(group => group.Key, group => group.First());
        var groups = new List<MetadataLocalOrderingGroupData>();
        var unresolved = new List<MetadataOrderingUnresolvedEpisode>();
        var special = false;
        for (var groupIndex = 0; groupIndex < ordering.Groups.Count; groupIndex++)
        {
            var group = ordering.Groups[groupIndex];
            var name = string.IsNullOrWhiteSpace(group.Name) ? $"Group {groupIndex + 1}" : group.Name.Trim();
            if (string.IsNullOrWhiteSpace(group.Name))
                notes.Add($"The group {groupIndex + 1} has no name, so it is named \"{name}\".");

            var found = new List<MetadataGuid>();
            foreach (var episode in group.Episodes)
            {
                var (shokoEpisode, reason) = ResolveEpisode(episode, series, byAnidbID, byNumber);
                if (shokoEpisode is null)
                {
                    unresolved.Add(new(groupIndex, name, episode.AnidbEpisodeId, episode.Type, episode.Number, reason!));
                    continue;
                }

                if (found.Contains(shokoEpisode.ID))
                {
                    notes.Add($"The episode {shokoEpisode.AnidbEpisodeID} is given twice in the group \"{name}\", and is kept once.");
                    continue;
                }

                found.Add(shokoEpisode.ID);
            }

            var isSpecial = group.IsSpecial && !special;
            if (group.IsSpecial && special)
                notes.Add($"The group \"{name}\" is a second special group, so it is kept as a regular one.");
            special |= isSpecial;
            groups.Add(new()
            {
                Name = name,
                Overview = string.IsNullOrWhiteSpace(group.Description) ? null : group.Description,
                IsSpecial = isSpecial,
                Episodes = found,
            });
        }

        return (groups, unresolved);
    }

    /// <summary>
    ///   Reads an ordering's networks, leaving out with a note each one that
    ///   is not a network's ID or is on a source this server does not know.
    /// </summary>
    /// <param name="ordering">The ordering.</param>
    /// <param name="notes">Gets the networks left out.</param>
    /// <returns>The networks, in order, each once, or <c>null</c> when the file predates them.</returns>
    internal static IReadOnlyList<MetadataGuid>? ResolveNetworks(OrderingDocumentOrdering ordering, List<string> notes)
    {
        if (ordering.Networks is not { } given)
            return null;

        var networks = new List<MetadataGuid>();
        foreach (var text in given)
        {
            if (!MetadataGuid.TryParse(text, out var network) || network.EntityType != MetadataEntityType.Network)
                notes.Add($"\"{text}\" does not name a network, so it is left out.");
            else if (!network.Source.IsRegistered)
                notes.Add($"The network \"{network}\" is on {network.Source.Value}, a source this server does not know, so it is left out.");
            else if (!networks.Contains(network))
                networks.Add(network);
        }

        return networks;
    }

    /// <summary>
    ///   Finds one episode of the payload in the series: by its AniDB episode,
    ///   or, when this server does not know that episode, by its type and
    ///   number, but only when it is of the series' anime or names no anime.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <param name="series">The series.</param>
    /// <param name="byAnidbID">The series' episodes by AniDB episode.</param>
    /// <param name="byNumber">The series' episodes by type and number.</param>
    /// <returns>The Shoko episode, or why it was not found.</returns>
    private (IShokoEpisode? Episode, string? Reason) ResolveEpisode(
        OrderingDocumentEpisode episode,
        IShokoSeries series,
        Dictionary<int, IShokoEpisode> byAnidbID,
        Dictionary<(EpisodeType, int), IShokoEpisode> byNumber
    )
    {
        if (episode.AnidbEpisodeId is int anidbEpisodeID && anidbEpisodeID > 0)
        {
            if (byAnidbID.TryGetValue(anidbEpisodeID, out var found))
                return (found, null);
            if (metadataService.GetShokoEpisodeByAnidbID(anidbEpisodeID) is { } elsewhere)
                return (null, $"The AniDB episode {anidbEpisodeID} belongs to another series, \"{elsewhere.SeriesID}\".");
        }

        // The type and number only tell an episode of the series' own anime.
        if (episode.AnidbAnimeId is int animeID && animeID > 0 && animeID != series.AnidbAnimeID)
            return (null, episode.AnidbEpisodeId is > 0
                ? $"The AniDB episode {episode.AnidbEpisodeId} belongs to AniDB anime {animeID}, which is not in the collection."
                : $"It belongs to AniDB anime {animeID}, which is not in the collection.");

        if (episode.Type is { } type && episode.Number is { } number)
            return byNumber.TryGetValue((type, number), out var byPlace)
                ? (byPlace, null)
                : (null, $"The series has no {type} {number}{(episode.AnidbEpisodeId is > 0 ? $" and no AniDB episode {episode.AnidbEpisodeId}" : string.Empty)}.");

        return (null, episode.AnidbEpisodeId is > 0
            ? $"The series has no AniDB episode {episode.AnidbEpisodeId}."
            : "It names neither an AniDB episode nor a type and number.");
    }

    /// <summary>
    ///   Whether two ordering names are the same, ignoring case and the space
    ///   around them.
    /// </summary>
    /// <param name="left">One name.</param>
    /// <param name="right">The other.</param>
    /// <returns><c>true</c> if they are.</returns>
    private static bool SameName(string left, string right)
        => string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///   A name no ordering of the series has: the name with <c>(2)</c>,
    ///   <c>(3)</c> and so on after it.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="taken">The names taken.</param>
    /// <returns>The free name.</returns>
    internal static string FreeName(string name, IReadOnlyCollection<string> taken)
    {
        for (var number = 2; ; number++)
        {
            var candidate = $"{name} ({number})";
            if (!taken.Any(other => SameName(other, candidate)))
                return candidate;
        }
    }

    /// <summary>
    ///   Removes the images linked to a replaced ordering itself, whose groups
    ///   took theirs with them.
    /// </summary>
    /// <param name="ordering">The ordering.</param>
    private void RemoveOwnImageLinks(IOrdering ordering)
    {
        foreach (var link in imageManager.GetImageCrossReferencesForEntity(ordering, new() { LinkedEntityImages = false }))
            imageManager.RemoveImageCrossReference(link);
    }

    #endregion

    #region Import | Images

    /// <summary>
    ///   A way to restore an image.
    /// </summary>
    private enum ImageWay
    {
        Payload,
        Url,
    }

    /// <summary>
    ///   The ways to try, in order, for an image mode.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <returns>The ways.</returns>
    private static ImageWay[] Ways(MetadataOrderingImageImportMode mode)
        => mode switch
        {
            MetadataOrderingImageImportMode.PayloadFirst => [ImageWay.Payload, ImageWay.Url],
            MetadataOrderingImageImportMode.UrlFirst => [ImageWay.Url, ImageWay.Payload],
            MetadataOrderingImageImportMode.PayloadOnly => [ImageWay.Payload],
            MetadataOrderingImageImportMode.UrlOnly => [ImageWay.Url],
            _ => [],
        };

    /// <summary>
    ///   Restores and links the images of an ordering or a group.
    /// </summary>
    /// <param name="entity">The stored ordering or group.</param>
    /// <param name="groupIndex">The group's place, or <c>null</c> for the ordering.</param>
    /// <param name="images">The images of the payload.</param>
    /// <param name="context">The import.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>What was done with each image.</returns>
    private async Task<List<MetadataOrderingImageImportEntry>> RestoreImages(
        IWithImages entity,
        int? groupIndex,
        IReadOnlyList<OrderingDocumentImage> images,
        ImportContext context,
        CancellationToken cancellationToken
    )
    {
        var results = new List<MetadataOrderingImageImportEntry>();
        foreach (var image in images)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await RestoreImage(entity, groupIndex, image, context).ConfigureAwait(false));
        }

        return results;
    }

    /// <summary>
    ///   Restores one image the ways the options allow, in order, and links
    ///   it to the entity.
    /// </summary>
    /// <param name="entity">The stored ordering or group.</param>
    /// <param name="groupIndex">The group's place, or <c>null</c> for the ordering.</param>
    /// <param name="image">The image of the payload.</param>
    /// <param name="context">The import.</param>
    /// <returns>What was done.</returns>
    private async Task<MetadataOrderingImageImportEntry> RestoreImage(IWithImages entity, int? groupIndex, OrderingDocumentImage image, ImportContext context)
    {
        var entry = new MetadataOrderingImageImportEntry { GroupIndex = groupIndex, ImageType = image.ImageType, Status = MetadataOrderingImageImportStatus.Skipped };
        var ways = Ways(context.Options.ImageMode);
        if (ways.Length is 0)
            return entry with { Reason = "Images were not asked for." };
        if (image.ImageType is ImageEntityType.None)
            return entry with { Status = MetadataOrderingImageImportStatus.Failed, Reason = "It has no image type." };

        var reasons = new List<string>();
        foreach (var way in ways)
        {
            var (restored, status, reason) = way is ImageWay.Payload
                ? FromPayload(image, context)
                : await FromUrl(image).ConfigureAwait(false);
            if (restored is null)
            {
                reasons.Add(reason!);
                continue;
            }

            try
            {
                Link(entity, restored, image);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or ImageCrossReferenceExistsException)
            {
                reasons.Add($"It could not be linked: {ex.Message}");
                return entry with { Status = MetadataOrderingImageImportStatus.Failed, ImageID = restored.ID, Reason = string.Join(" ", reasons) };
            }

            return entry with { Status = status, ImageID = restored.ID, Reason = reasons.Count is 0 ? null : string.Join(" ", reasons) };
        }

        return entry with { Status = MetadataOrderingImageImportStatus.Failed, Reason = string.Join(" ", reasons) };
    }

    /// <summary>
    ///   Restores an image from its file in the payload: as the held copy of
    ///   its remote image when it names a source this server can fetch from,
    ///   so it keeps its source and URL, or else as a user's image.
    /// </summary>
    /// <param name="image">The image of the payload.</param>
    /// <param name="context">The import.</param>
    /// <returns>The image and its status, or why it could not be restored.</returns>
    private (IImage? Image, MetadataOrderingImageImportStatus Status, string? Reason) FromPayload(OrderingDocumentImage image, ImportContext context)
    {
        if (CheckedFile(image, context, out var problem) is not { } bytes)
            return (null, MetadataOrderingImageImportStatus.Failed, problem);

        if (RemoteOf(image, out _) is { } remote)
        {
            if (RemoteImage(remote, image, out problem) is not { } stored)
                return (null, MetadataOrderingImageImportStatus.Failed, problem);
            if (stored.IsAvailable)
                return (stored, MetadataOrderingImageImportStatus.FromPayload, null);

            try
            {
                return (imageFiles.StoreFile(stored, bytes), MetadataOrderingImageImportStatus.FromPayload, null);
            }
            catch (Exception ex) when (ex is ArgumentException or UnsupportedImageTypeException or IOException or UnauthorizedAccessException)
            {
                return (null, MetadataOrderingImageImportStatus.Failed, $"Its file was refused: {ex.Message}");
            }
        }

        IImage uploaded;
        try
        {
            uploaded = imageManager.UploadImage(bytes, null, userSubmitted: true);
        }
        catch (Exception ex) when (ex is ArgumentException or UnsupportedImageTypeException)
        {
            return (null, MetadataOrderingImageImportStatus.Failed, $"Its file was refused: {ex.Message}");
        }

        if (string.IsNullOrEmpty(uploaded.LanguageCode) && image.Language is { Length: > 0 and <= 5 } language)
        {
            try
            {
                uploaded = imageManager.UpdateImage(uploaded, new() { LanguageCode = language });
            }
            catch (ArgumentException ex)
            {
                logger.LogDebug(ex, "Unable to set the language of the imported image {ImageID}.", uploaded.ID);
            }
        }

        return (uploaded, MetadataOrderingImageImportStatus.FromPayload, null);
    }

    /// <summary>
    ///   Reads an image's file from the payload and checks its hash when the
    ///   options ask.
    /// </summary>
    /// <param name="image">The image of the payload.</param>
    /// <param name="context">The import.</param>
    /// <param name="problem">Why there is no usable file, when there is none.</param>
    /// <returns>The file, or <c>null</c>.</returns>
    private static byte[]? CheckedFile(OrderingDocumentImage image, ImportContext context, out string? problem)
    {
        if (context.Payload.GetFile(image, out problem) is not { } bytes)
            return null;

        if (context.Options.VerifyHashes && !string.IsNullOrEmpty(image.Sha256) && !string.Equals(Sha256(bytes), image.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            problem = "Its file's SHA-256 does not match the one given.";
            return null;
        }

        return bytes;
    }

    /// <summary>
    ///   Restores an image from its remote source: the image this server has
    ///   for it, or a new one with its download queued.
    /// </summary>
    /// <param name="image">The image of the payload.</param>
    /// <returns>The image and its status, or why it could not be restored.</returns>
    private async Task<(IImage? Image, MetadataOrderingImageImportStatus Status, string? Reason)> FromUrl(OrderingDocumentImage image)
    {
        if (RemoteOf(image, out var problem) is not { } remote)
            return (null, MetadataOrderingImageImportStatus.Failed, problem);

        if (RemoteImage(remote, image, out problem) is not { } stored)
            return (null, MetadataOrderingImageImportStatus.Failed, problem);

        if (stored.IsAvailable)
            return (stored, MetadataOrderingImageImportStatus.FromUrl, null);

        await imageManager.ScheduleDownloadOfImage(stored).ConfigureAwait(false);
        return (stored, MetadataOrderingImageImportStatus.Pending, null);
    }

    /// <summary>
    ///   The image this server has for a remote source's resource, added
    ///   when it has none yet.
    /// </summary>
    /// <param name="remote">The source and resource ID.</param>
    /// <param name="image">The image of the payload, for its size and language.</param>
    /// <param name="problem">Why there is none, when there is none.</param>
    /// <returns>The image, or <c>null</c>.</returns>
    private IImage? RemoteImage((MetadataSource Source, string ResourceID) remote, OrderingDocumentImage image, out string? problem)
    {
        problem = null;
        var (source, resourceID) = remote;
        if (imageManager.GetImageBySourceAndRemoteResourceID(source, resourceID) is { } stored)
            return stored;

        try
        {
            stored = imageManager.AddImage(new()
            {
                Source = source,
                ResourceID = resourceID,
                Width = image.Width is > 0 && image.Height is > 0 ? image.Width : null,
                Height = image.Width is > 0 && image.Height is > 0 ? image.Height : null,
                LanguageCode = image.Language is { Length: > 0 and <= 5 } language ? language : null,
            });
        }
        catch (ImageDataExistsException)
        {
            stored = imageManager.GetImageBySourceAndRemoteResourceID(source, resourceID);
        }
        catch (Exception ex) when (ex is UnsupportedImageTypeException or MissingImageSourceTemplateUrlException or ArgumentException)
        {
            problem = $"It could not be added from {source.Value}: {ex.Message}";
            return null;
        }

        if (stored is null)
            problem = $"It could not be added from {source.Value}.";
        return stored;
    }

    /// <summary>
    ///   The remote source and resource ID of an image of the payload: the
    ///   ones it gives, when this server knows the source, or else the source
    ///   whose template URL its URL fits.
    /// </summary>
    /// <param name="image">The image of the payload.</param>
    /// <param name="problem">Why there is none, when there is none.</param>
    /// <returns>The source and resource ID, or <c>null</c>.</returns>
    private (MetadataSource Source, string ResourceID)? RemoteOf(OrderingDocumentImage image, out string? problem)
    {
        problem = null;
        if (!string.IsNullOrWhiteSpace(image.Source) && MetadataSource.TryGet(image.Source, out var source) && source.IsRemote)
        {
            if (string.IsNullOrEmpty(imageManager.GetTemplateUrlForSource(source)))
            {
                problem = $"This server has no template URL for {source.Value}.";
                return null;
            }

            if (!string.IsNullOrEmpty(image.ResourceId))
                return (source, image.ResourceId);
            if (!string.IsNullOrEmpty(image.Url) && ResourceIDOf(imageManager.GetTemplateUrlForSource(source)!, image.Url) is { } fromUrl)
                return (source, fromUrl);
        }

        if (string.IsNullOrWhiteSpace(image.Url))
        {
            problem = string.IsNullOrWhiteSpace(image.Source) ? "It has no remote URL." : $"This server does not know the source \"{image.Source}\" and it has no URL.";
            return null;
        }

        foreach (var (templateSource, template) in imageManager.GetTemplateUrls())
        {
            if (!string.IsNullOrEmpty(template) && ResourceIDOf(template, image.Url) is { } resourceID)
                return (templateSource, resourceID);
        }

        problem = $"No image source on this server serves \"{image.Url}\".";
        return null;
    }

    /// <summary>
    ///   The resource ID that completes a template URL into a URL.
    /// </summary>
    /// <param name="template">The template, with one <c>{0}</c>.</param>
    /// <param name="url">The URL.</param>
    /// <returns>The resource ID, or <c>null</c> when the URL does not fit.</returns>
    internal static string? ResourceIDOf(string template, string url)
    {
        var slot = template.IndexOf("{0}", StringComparison.Ordinal);
        if (slot < 0)
            return null;

        var prefix = template[..slot];
        var suffix = template[(slot + 3)..];
        if (url.Length <= prefix.Length + suffix.Length ||
            !url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !url.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return null;

        return url[prefix.Length..^suffix.Length];
    }

    /// <summary>
    ///   Links a restored image to an ordering or group, preferred when the
    ///   payload says so.
    /// </summary>
    /// <param name="entity">The ordering or group.</param>
    /// <param name="image">The restored image.</param>
    /// <param name="payload">The image of the payload.</param>
    private void Link(IWithImages entity, IImage image, OrderingDocumentImage payload)
    {
        var existing = imageManager.GetImageCrossReferencesForEntity(entity, new() { ImageType = payload.ImageType, LinkedEntityImages = false })
            .FirstOrDefault(link => link.ImageID == image.ID);
        if (existing is null)
            imageManager.AddImageCrossReference(entity, image, new()
            {
                ImageType = payload.ImageType,
                IsPreferred = payload.IsPreferred,
                IsEnabled = true,
                IsDesired = true,
            });
        else if (payload.IsPreferred && !existing.IsPreferred)
            imageManager.SetPreferredImageForEntity(existing);
    }

    /// <summary>
    ///   Works out what a dry run would do with the images of an ordering and
    ///   its groups, writing nothing.
    /// </summary>
    /// <param name="ordering">The ordering of the payload.</param>
    /// <param name="context">The import.</param>
    /// <returns>What would be done with each image.</returns>
    private IEnumerable<MetadataOrderingImageImportEntry> PlanImages(OrderingDocumentOrdering ordering, ImportContext context)
    {
        foreach (var image in ordering.Images)
            yield return PlanImage(null, image, context);
        for (var groupIndex = 0; groupIndex < ordering.Groups.Count; groupIndex++)
        {
            foreach (var image in ordering.Groups[groupIndex].Images)
                yield return PlanImage(groupIndex, image, context);
        }
    }

    /// <summary>
    ///   Works out what would be done with one image, writing nothing.
    /// </summary>
    /// <param name="groupIndex">The group's place, or <c>null</c> for the ordering.</param>
    /// <param name="image">The image of the payload.</param>
    /// <param name="context">The import.</param>
    /// <returns>What would be done.</returns>
    private MetadataOrderingImageImportEntry PlanImage(int? groupIndex, OrderingDocumentImage image, ImportContext context)
    {
        var entry = new MetadataOrderingImageImportEntry { GroupIndex = groupIndex, ImageType = image.ImageType, Status = MetadataOrderingImageImportStatus.Skipped };
        var ways = Ways(context.Options.ImageMode);
        if (ways.Length is 0)
            return entry with { Reason = "Images were not asked for." };
        if (image.ImageType is ImageEntityType.None)
            return entry with { Status = MetadataOrderingImageImportStatus.Failed, Reason = "It has no image type." };

        var reasons = new List<string>();
        foreach (var way in ways)
        {
            if (way is ImageWay.Payload)
            {
                if (CheckedFile(image, context, out var problem) is not null)
                    return entry with { Status = MetadataOrderingImageImportStatus.FromPayload, Reason = reasons.Count is 0 ? null : string.Join(" ", reasons) };
                reasons.Add(problem!);
                continue;
            }

            if (RemoteOf(image, out var remoteProblem) is not { } remote)
            {
                reasons.Add(remoteProblem!);
                continue;
            }

            var held = imageManager.GetImageBySourceAndRemoteResourceID(remote.Source, remote.ResourceID);
            return entry with
            {
                Status = held is { IsAvailable: true } ? MetadataOrderingImageImportStatus.FromUrl : MetadataOrderingImageImportStatus.Pending,
                ImageID = held?.ID,
                Reason = reasons.Count is 0 ? null : string.Join(" ", reasons),
            };
        }

        return entry with { Status = MetadataOrderingImageImportStatus.Failed, Reason = string.Join(" ", reasons) };
    }

    #endregion
}
