using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Image.Options;
using Shoko.Abstractions.Metadata.Orderings;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.Services;
using Shoko.Server.Services.OrderingTransfer;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers <see cref="MetadataOrderingTransferService"/> over the real ordering
/// service on in-memory tables and an image manager kept in memory: the file
/// format and its two containers, the translation of series and episodes to
/// and from AniDB IDs, what an import does with an ordering of the same name,
/// and where it restores each image from. An ordering's networks read
/// themselves through <c>RepoFactory</c>, so these tests share its collection.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class MetadataOrderingTransferServiceTests
{
    #region Harness

    private const string TmdbTemplate = "https://image.tmdb.org/t/p/original{0}";

    private static readonly byte[] _posterFile = [0x89, (byte)'P', (byte)'N', (byte)'G', 1, 2, 3, 4, 5, 6, 7, 8];

    private static readonly byte[] _discFile = [0xFF, 0xD8, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0];

    /// <summary>
    /// An image the fake image manager holds.
    /// </summary>
    private sealed class FakeImage
    {
        public required Guid ID { get; init; }

        public required MetadataSource Source { get; init; }

        public required string ResourceID { get; init; }

        public byte[]? File { get; set; }

        public string? LanguageCode { get; set; }

        public int? Width { get; init; }

        public int? Height { get; init; }

        public string ContentType { get; init; } = "image/png";

        public IImage AsImage()
        {
            var image = new Mock<IImage>();
            image.SetupGet(value => value.ID).Returns(ID);
            image.SetupGet(value => value.Source).Returns(Source);
            image.SetupGet(value => value.ResourceID).Returns(ResourceID);
            image.SetupGet(value => value.IsAvailable).Returns(() => File is not null);
            image.SetupGet(value => value.LanguageCode).Returns(() => LanguageCode);
            image.SetupGet(value => value.Width).Returns(Width);
            image.SetupGet(value => value.Height).Returns(Height);
            image.SetupGet(value => value.ContentType).Returns(ContentType);
            image.Setup(value => value.GetStream()).Returns(() => File is null ? null : new MemoryStream(File));
            return image.Object;
        }
    }

    /// <summary>
    /// A link the fake image manager holds.
    /// </summary>
    private sealed class FakeLink
    {
        public required MetadataGuid EntityID { get; init; }

        public required Guid ImageID { get; init; }

        public required ImageEntityType ImageType { get; init; }

        public bool IsPreferred { get; set; }

        public IImageCrossReference Reference { get; }

        public FakeLink()
        {
            var link = new Mock<IImageCrossReference>();
            link.SetupGet(value => value.EntityID).Returns(() => EntityID!);
            link.SetupGet(value => value.ImageID).Returns(() => ImageID);
            link.SetupGet(value => value.ImageType).Returns(() => ImageType);
            link.SetupGet(value => value.IsPreferred).Returns(() => IsPreferred);
            link.SetupGet(value => value.IsEnabled).Returns(true);
            Reference = link.Object;
        }
    }

    /// <summary>
    /// A server: Shoko series and episodes, the real ordering service over
    /// in-memory tables, a fake image manager and the transfer service.
    /// </summary>
    private sealed class World
    {
        private readonly Dictionary<MetadataGuid, IMetadata> _entries = [];

        private readonly Dictionary<int, IShokoSeries> _seriesByAnime = [];

        private readonly Dictionary<int, IShokoEpisode> _episodesByAnidb = [];

        public Mock<IMetadataService> Metadata { get; } = new();

        public Mock<IImageManager> Images { get; } = new();

        public Mock<IImageFileStore> Files { get; } = new();

        public Dictionary<Guid, FakeImage> ImageStore { get; } = [];

        public List<FakeLink> Links { get; } = [];

        public List<Guid> Downloads { get; } = [];

        public OrderingTables Tables { get; } = new();

        public MetadataOrderingService Orderings { get; }

        public MetadataOrderingTransferService Service { get; }

        public World()
        {
            Metadata.Setup(metadata => metadata.GetSeries(It.IsAny<MetadataGuid>())).Returns((MetadataGuid id) => _entries.GetValueOrDefault(id) as ISeries);
            Metadata.Setup(metadata => metadata.GetEpisode(It.IsAny<MetadataGuid>())).Returns((MetadataGuid id) => _entries.GetValueOrDefault(id) as IEpisode);
            Metadata.Setup(metadata => metadata.GetShokoSeriesByAnidbID(It.IsAny<int>())).Returns((int id) => _seriesByAnime.GetValueOrDefault(id));
            Metadata.Setup(metadata => metadata.GetShokoEpisodeByAnidbID(It.IsAny<int>())).Returns((int id) => _episodesByAnidb.GetValueOrDefault(id));
            SetUpImages();
            Orderings = Tables.Build(() => Metadata.Object);
            var system = new Mock<ISystemService>();
            system.SetupGet(value => value.Version).Returns(new VersionInformation
            {
                Version = new(5, 3, 1),
                RuntimeIdentifier = "linux-x64",
                AbstractionVersion = new(6, 0, 0),
                SourceRevision = null,
                ReleaseTag = null,
                Channel = ReleaseChannel.Debug,
                ReleasedAt = DateTime.UnixEpoch,
            });
            Service = new(Orderings, Metadata.Object, Images.Object, Files.Object, Tables.StudioStore, system.Object, NullLogger<MetadataOrderingTransferService>.Instance);
        }

        private void SetUpImages()
        {
            Images.Setup(images => images.GetTemplateUrlForSource(It.IsAny<MetadataSource>()))
                .Returns((MetadataSource source) => source == MetadataSource.TMDB ? TmdbTemplate : null);
            Images.Setup(images => images.GetTemplateUrls())
                .Returns(new Dictionary<MetadataSource, string?> { [MetadataSource.TMDB] = TmdbTemplate, [MetadataSource.AniDB] = null });
            Images.Setup(images => images.GetImageByID(It.IsAny<Guid>(), It.IsAny<bool>()))
                .Returns((Guid id, bool _) => ImageStore.GetValueOrDefault(id)?.AsImage());
            Images.Setup(images => images.GetImageBySourceAndRemoteResourceID(It.IsAny<MetadataSource>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns((MetadataSource source, string resourceID, bool _) => ImageStore.Values.FirstOrDefault(image => image.Source == source && image.ResourceID == resourceID)?.AsImage());
            Images.Setup(images => images.AddImage(It.IsAny<ImageData>()))
                .Returns((ImageData data) => Store(new() { ID = Guid.NewGuid(), Source = data.Source, ResourceID = data.ResourceID, LanguageCode = data.LanguageCode }));
            Images.Setup(images => images.UploadImage(It.IsAny<byte[]>(), It.IsAny<string?>(), It.IsAny<bool>()))
                .Returns((byte[] bytes, string? _, bool _) =>
                {
                    var md5 = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(bytes));
                    return ImageStore.Values.FirstOrDefault(image => image.Source == MetadataSource.User && image.ResourceID == md5)?.AsImage()
                        ?? Store(new() { ID = Guid.NewGuid(), Source = MetadataSource.User, ResourceID = md5, File = bytes });
                });
            Images.Setup(images => images.UpdateImage(It.IsAny<IImage>(), It.IsAny<ImageUpdateData>()))
                .Returns((IImage image, ImageUpdateData data) =>
                {
                    ImageStore[image.ID].LanguageCode = data.LanguageCode;
                    return ImageStore[image.ID].AsImage();
                });
            Images.Setup(images => images.ScheduleDownloadOfImage(It.IsAny<IImage>(), It.IsAny<bool>()))
                .Callback((IImage image, bool _) => Downloads.Add(image.ID))
                .Returns(Task.CompletedTask);
            Images.Setup(images => images.DownloadImage(It.IsAny<IImage>(), It.IsAny<bool>())).ReturnsAsync(false);
            Files.Setup(files => files.StoreFile(It.IsAny<IImage>(), It.IsAny<byte[]>()))
                .Returns((IImage image, byte[] file) =>
                {
                    ImageStore[image.ID].File = file;
                    return ImageStore[image.ID].AsImage();
                });
            Images.Setup(images => images.GetImageCrossReferencesForEntity(It.IsAny<IWithImages>(), It.IsAny<ImageCrossReferenceFilteringOptions?>()))
                .Returns((IWithImages entity, ImageCrossReferenceFilteringOptions? options) =>
                [
                    .. Links
                        .Where(link => link.EntityID == entity.ID && (options?.ImageType is null || link.ImageType == options.ImageType))
                        .Select(link => link.Reference),
                ]);
            Images.Setup(images => images.AddImageCrossReference(It.IsAny<IWithImages>(), It.IsAny<IImage>(), It.IsAny<ImageCrossReferenceData>()))
                .Returns((IWithImages entity, IImage image, ImageCrossReferenceData data) =>
                {
                    if (data.IsPreferred)
                        foreach (var sibling in Links.Where(link => link.EntityID == entity.ID && link.ImageType == data.ImageType))
                            sibling.IsPreferred = false;
                    var link = new FakeLink { EntityID = entity.ID, ImageID = image.ID, ImageType = data.ImageType, IsPreferred = data.IsPreferred };
                    Links.Add(link);
                    return link.Reference;
                });
            Images.Setup(images => images.SetPreferredImageForEntity(It.IsAny<IImageCrossReference>()))
                .Returns((IImageCrossReference reference) =>
                {
                    var link = Links.Single(link => link.Reference == reference);
                    foreach (var sibling in Links.Where(other => other.EntityID == link.EntityID && other.ImageType == link.ImageType))
                        sibling.IsPreferred = sibling == link;
                    return reference;
                });
            Images.Setup(images => images.RemoveImageCrossReference(It.IsAny<IImageCrossReference>()))
                .Returns((IImageCrossReference reference) => Links.RemoveAll(link => link.Reference == reference) > 0);

            // What the ordering service removes goes through the tables' image
            // manager, so both look at the same links.
            Tables.Images.Setup(images => images.GetImageCrossReferencesForEntity(It.IsAny<IWithImages>(), It.IsAny<ImageCrossReferenceFilteringOptions?>()))
                .Returns((IWithImages entity, ImageCrossReferenceFilteringOptions? options) => Images.Object.GetImageCrossReferencesForEntity(entity, options));
            Tables.Images.Setup(images => images.RemoveImageCrossReference(It.IsAny<IImageCrossReference>()))
                .Returns((IImageCrossReference reference) => Images.Object.RemoveImageCrossReference(reference));
        }

        public IImage Store(FakeImage image)
        {
            ImageStore[image.ID] = image;
            return image.AsImage();
        }

        public void Link(IWithImages entity, IImage image, ImageEntityType type, bool preferred = false)
            => Images.Object.AddImageCrossReference(entity, image, new() { ImageType = type, IsPreferred = preferred });

        public IEnumerable<FakeLink> LinksOf(MetadataGuid entity)
            => Links.Where(link => link.EntityID == entity);

        /// <summary>
        /// Adds a Shoko series for an AniDB anime, with its episodes as
        /// (Shoko ID, AniDB episode ID, type, number).
        /// </summary>
        public IShokoSeries AddSeries(int localID, int animeID, string title, params (int ShokoID, int AnidbID, EpisodeType Type, int Number)[] episodes)
        {
            var seriesID = new MetadataGuid(MetadataSource.Shoko, MetadataEntityType.Series, localID.ToString());
            var series = new Mock<IShokoSeries>();
            var shokoEpisodes = new List<IShokoEpisode>();
            foreach (var (shokoID, anidbID, type, number) in episodes)
            {
                var episode = new Mock<IShokoEpisode>();
                episode.SetupGet(value => value.ID).Returns(new MetadataGuid(MetadataSource.Shoko, MetadataEntityType.Episode, shokoID.ToString()));
                episode.SetupGet(value => value.SeriesID).Returns(seriesID);
                episode.SetupGet(value => value.ShokoSeriesID).Returns(localID);
                episode.SetupGet(value => value.AnidbEpisodeID).Returns(anidbID);
                episode.SetupGet(value => value.Type).Returns(type);
                episode.SetupGet(value => value.EpisodeNumber).Returns(number);
                episode.SetupGet(value => value.Series).Returns(() => series.Object);
                episode.As<IEpisode>().SetupGet(value => value.Series).Returns(() => series.Object);
                episode.As<IEpisode>().SetupGet(value => value.ShokoEpisodes).Returns(() => [episode.Object]);
                shokoEpisodes.Add(episode.Object);
                _entries[episode.Object.ID] = episode.Object;
                _episodesByAnidb[anidbID] = episode.Object;
            }

            series.SetupGet(value => value.ID).Returns(seriesID);
            series.SetupGet(value => value.LocalID).Returns(localID);
            series.SetupGet(value => value.AnidbAnimeID).Returns(animeID);
            series.SetupGet(value => value.Title).Returns(title);
            series.SetupGet(value => value.Episodes).Returns(shokoEpisodes);
            series.As<ISeries>().SetupGet(value => value.Episodes).Returns(shokoEpisodes);
            series.As<ISeries>().SetupGet(value => value.Seasons).Returns([]);
            _entries[seriesID] = series.Object;
            _seriesByAnime[animeID] = series.Object;
            return series.Object;
        }

        /// <summary>
        /// Adds a series of another source whose episodes are linked to Shoko
        /// episodes, or to none.
        /// </summary>
        public ISeries AddLinkedSeries(MetadataSource source, string id, params (string ID, IShokoEpisode[] Linked)[] episodes)
        {
            var seriesID = new MetadataGuid(source, MetadataEntityType.Series, id);
            var series = new Mock<ISeries>();
            var list = new List<IEpisode>();
            foreach (var (episodeID, linked) in episodes)
            {
                var episode = new Mock<IEpisode>();
                episode.SetupGet(value => value.ID).Returns(new MetadataGuid(source, MetadataEntityType.Episode, episodeID));
                episode.SetupGet(value => value.SeriesID).Returns(seriesID);
                episode.SetupGet(value => value.ShokoEpisodes).Returns(linked);
                episode.SetupGet(value => value.Series).Returns(() => series.Object);
                list.Add(episode.Object);
                _entries[episode.Object.ID] = episode.Object;
            }

            series.SetupGet(value => value.ID).Returns(seriesID);
            series.SetupGet(value => value.Title).Returns(id);
            series.SetupGet(value => value.Episodes).Returns(list);
            series.SetupGet(value => value.Seasons).Returns([]);
            series.SetupGet(value => value.ShokoSeries).Returns([]);
            _entries[seriesID] = series.Object;
            return series.Object;
        }

        public static MetadataGuid Episode(int shokoID)
            => new(MetadataSource.Shoko, MetadataEntityType.Episode, shokoID.ToString());

        public async Task<(byte[] Content, MetadataOrderingExportResult Result)> Export(MetadataOrderingExportOptions? options = null)
        {
            var file = await ((IMetadataOrderingTransferService)Service).ExportToBytes(options, TestContext.Current.CancellationToken);
            return (file.Content, file.Result);
        }

        public Task<MetadataOrderingImportResult> Import(byte[] content, MetadataOrderingImportOptions? options = null)
            => Service.Import(new MemoryStream(content), options, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The series both servers have: AniDB anime 100 with three episodes and
    /// a special, under Shoko IDs that differ between the two.
    /// </summary>
    private static IShokoSeries AddAnime100(World world, int offset = 0)
        => world.AddSeries(
            1 + offset,
            100,
            "Anime 100",
            (10 + offset, 1001, EpisodeType.Episode, 1),
            (11 + offset, 1002, EpisodeType.Episode, 2),
            (12 + offset, 1003, EpisodeType.Episode, 3),
            (13 + offset, 1004, EpisodeType.Special, 1)
        );

    /// <summary>
    /// A local ordering of anime 100 with a special group, a poster uploaded
    /// by a user on the ordering, and a TMDB backdrop on its first group.
    /// </summary>
    private static IOrdering AddDvdOrdering(World world, IShokoSeries series)
    {
        var ordering = world.Orderings.CreateLocalOrdering(new()
        {
            SeriesID = series.ID,
            Name = "DVD Order",
            Overview = "As on the discs.",
            Groups =
            [
                new() { Name = "Disc 1", Overview = "The first disc.", Episodes = [series.Episodes[1].ID, series.Episodes[0].ID] },
                new() { Name = "Extras", IsSpecial = true, Episodes = [series.Episodes[3].ID] },
                new() { Name = "Disc 2", Episodes = [series.Episodes[2].ID] },
            ],
        });
        var poster = world.Images.Object.UploadImage(_posterFile);
        world.Link(ordering, poster, ImageEntityType.Primary, preferred: true);
        var backdrop = world.Store(new() { ID = Guid.NewGuid(), Source = MetadataSource.TMDB, ResourceID = "/backdrop.jpg", LanguageCode = "en", Width = 1920, Height = 1080 });
        world.Link(ordering.Seasons[0], backdrop, ImageEntityType.Backdrop);
        return world.Orderings.GetOrdering(ordering.ID)!;
    }

    private static OrderingDocument ReadJson(byte[] content)
        => OrderingDocumentSerializer.Deserialize(new MemoryStream(content), TestContext.Current.CancellationToken).GetAwaiter().GetResult();

    private static OrderingDocument ReadZip(byte[] content, out ZipArchive archive)
    {
        archive = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read);
        using var manifest = archive.GetEntry(OrderingDocumentSerializer.ManifestName)!.Open();
        return OrderingDocumentSerializer.Deserialize(manifest, TestContext.Current.CancellationToken).GetAwaiter().GetResult();
    }

    private static byte[] Json(OrderingDocument document)
        => Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(document, OrderingDocumentSerializer.JsonOptions));

    private static OrderingDocument Document(params OrderingDocumentOrdering[] orderings)
        => new() { Format = OrderingDocument.FormatName, Version = OrderingDocument.CurrentVersion, ExportedAt = DateTime.UtcNow, Orderings = [.. orderings] };

    private static OrderingDocumentOrdering Ordering(int animeID, string name, params OrderingDocumentGroup[] groups)
        => new() { Series = new() { AnidbAnimeId = animeID }, Name = name, Type = OrderingType.User, Groups = [.. groups] };

    private static OrderingDocumentGroup Group(string name, params int[] anidbEpisodeIDs)
        => new() { Name = name, Episodes = [.. anidbEpisodeIDs.Select(id => new OrderingDocumentEpisode { AnidbEpisodeId = id })] };

    #endregion

    #region Format

    [Fact]
    public async Task AJsonExportHoldsTheOrderingByAnidbIDsWithItsImages()
    {
        var world = new World();
        var series = AddAnime100(world);
        var ordering = AddDvdOrdering(world, series);
        world.Orderings.SetPreferredOrdering(series.ID, ordering.ID);

        var (content, result) = await world.Export(new() { ImageMode = MetadataOrderingImageExportMode.EmbedMissingRemote, Container = MetadataOrderingContainer.Json });

        Assert.Equal(MetadataOrderingContainer.Json, result.Container);
        Assert.Equal((1, 2, 1), (result.OrderingCount, result.ImageCount, result.EmbeddedImageCount));
        Assert.Empty(result.Warnings);
        var document = ReadJson(content);
        Assert.Equal(("shoko-orderings", 1), (document.Format, document.Version));
        var written = Assert.Single(document.Orderings);
        Assert.Equal((100, "Anime 100"), (written.Series!.AnidbAnimeId, written.Series.Title));
        Assert.True(written.IsPreferred);
        Assert.Equal(ordering.ID.ToString(), written.Origin);
        Assert.Equal(["Disc 1", "Extras", "Disc 2"], written.Groups.Select(group => group.Name));
        Assert.Equal([false, true, false], written.Groups.Select(group => group.IsSpecial));
        Assert.Equal("The first disc.", written.Groups[0].Description);
        Assert.Equal([1002, 1001], written.Groups[0].Episodes.Select(episode => episode.AnidbEpisodeId!.Value));
        Assert.Equal((EpisodeType.Special, 1), (written.Groups[1].Episodes[0].Type!.Value, written.Groups[1].Episodes[0].Number!.Value));

        // The uploaded poster has no remote source, so its file is inline.
        var poster = Assert.Single(written.Images);
        Assert.Equal((ImageEntityType.Primary, true), (poster.ImageType, poster.IsPreferred));
        Assert.Null(poster.Source);
        Assert.Null(poster.Url);
        Assert.Equal(_posterFile, Convert.FromBase64String(poster.Data!));
        Assert.Equal(MetadataOrderingTransferService.Sha256(_posterFile), poster.Sha256);

        // The backdrop is TMDB's, so it goes by URL.
        var backdrop = Assert.Single(written.Groups[0].Images);
        Assert.Equal(("tmdb", "/backdrop.jpg", "https://image.tmdb.org/t/p/original/backdrop.jpg"), (backdrop.Source, backdrop.ResourceId, backdrop.Url));
        Assert.Equal(("en", 1920, 1080), (backdrop.Language, backdrop.Width!.Value, backdrop.Height!.Value));
        Assert.Null(backdrop.Data);
        Assert.Null(backdrop.Sha256);

        // The JSON is camel case, with enums as text.
        var text = Encoding.UTF8.GetString(content);
        Assert.Contains("\"anidbAnimeId\": 100", text);
        Assert.Contains("\"imageType\": \"backdrop\"", text);
    }

    [Fact]
    public async Task AZipExportHoldsTheManifestAndEachEmbeddedFileByItsHash()
    {
        var world = new World();
        var series = AddAnime100(world);
        var ordering = AddDvdOrdering(world, series);
        var disc = world.Store(new() { ID = Guid.NewGuid(), Source = MetadataSource.TMDB, ResourceID = "/disc.jpg", File = _discFile, ContentType = "image/jpeg" });
        world.Link(ordering.Seasons[2], disc, ImageEntityType.Disc);

        var (content, result) = await world.Export(new() { ImageMode = MetadataOrderingImageExportMode.EmbedAll });

        Assert.Equal(MetadataOrderingContainer.Zip, result.Container);
        Assert.Equal((".zip", "application/zip"), (result.FileExtension, result.ContentType));
        Assert.Equal((3, 2), (result.ImageCount, result.EmbeddedImageCount));
        // The backdrop is not held and could not be downloaded, so it is written by URL only.
        Assert.Single(result.Warnings, warning => warning.Contains("URL only"));
        var document = ReadZip(content, out var archive);
        using (archive)
        {
            var discEntry = document.Orderings[0].Groups[2].Images.Single();
            var sha = MetadataOrderingTransferService.Sha256(_discFile);
            Assert.Equal($"images/{sha}.jpg", discEntry.File);
            Assert.Equal(sha, discEntry.Sha256);
            Assert.Equal("https://image.tmdb.org/t/p/original/disc.jpg", discEntry.Url);
            using var stream = archive.GetEntry(discEntry.File!)!.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            Assert.Equal(_discFile, buffer.ToArray());
            Assert.Equal(
                [$"images/{MetadataOrderingTransferService.Sha256(_posterFile)}.png", discEntry.File, OrderingDocumentSerializer.ManifestName],
                archive.Entries.Select(entry => entry.FullName)
            );
        }
    }

    /// <summary>
    /// A stream that can only be written forwards, as a response body.
    /// </summary>
    private sealed class ForwardOnlyStream(Stream inner) : Stream
    {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() => inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
    }

    [Fact]
    public async Task AZipExportIsWrittenForwardsWithEachFileOnce()
    {
        var world = new World();
        var series = AddAnime100(world);
        var ordering = AddDvdOrdering(world, series);
        var poster = Assert.Single(world.LinksOf(ordering.ID));
        world.Link(ordering.Seasons[2], world.ImageStore[poster.ImageID].AsImage(), ImageEntityType.Primary);

        using var buffer = new MemoryStream();
        var result = await world.Service.Export(
            new ForwardOnlyStream(buffer),
            new() { ImageMode = MetadataOrderingImageExportMode.EmbedMissingRemote },
            TestContext.Current.CancellationToken
        );

        Assert.Equal((MetadataOrderingContainer.Zip, 2), (result.Container, result.EmbeddedImageCount));
        var document = ReadZip(buffer.ToArray(), out var archive);
        using (archive)
        {
            var path = $"images/{MetadataOrderingTransferService.Sha256(_posterFile)}.png";
            Assert.Equal(path, document.Orderings[0].Images.Single().File);
            Assert.Equal(path, document.Orderings[0].Groups[2].Images.Single().File);
            Assert.Equal([path, OrderingDocumentSerializer.ManifestName], archive.Entries.Select(entry => entry.FullName));
        }
    }

    [Fact]
    public async Task AnExportImportedIntoAnotherServerComesBackWhole()
    {
        var source = new World();
        var ordering = AddDvdOrdering(source, AddAnime100(source));
        var (content, _) = await source.Export(new() { ImageMode = MetadataOrderingImageExportMode.EmbedMissingRemote });

        // The other server has the same anime under other Shoko IDs.
        var target = new World();
        var series = AddAnime100(target, offset: 50);
        var result = await target.Import(content);

        Assert.True(result.Succeeded);
        var entry = Assert.Single(result.Orderings);
        Assert.Equal(MetadataOrderingImportOutcome.Created, entry.Outcome);
        Assert.Equal(series.ID, entry.SeriesID);
        Assert.Empty(entry.UnresolvedEpisodes);
        var imported = target.Orderings.GetOrdering(entry.OrderingID!)!;
        Assert.Equal(MetadataSource.User, imported.ID.Source);
        Assert.Equal(("DVD Order", "As on the discs."), (imported.Name, imported.Overview));
        Assert.Equal(ordering.Seasons.Select(group => group.Title), imported.Seasons.Select(group => group.Title));
        Assert.Equal([false, true, false], imported.Seasons.Select(group => group.IsSpecial));
        Assert.Equal([World.Episode(61), World.Episode(60)], imported.Seasons[0].Episodes.Select(episode => episode.ID));
        Assert.Equal([World.Episode(63)], imported.Seasons[1].Episodes.Select(episode => episode.ID));
        Assert.Equal("The first disc.", imported.Seasons[0].DefaultOverview?.Value);

        // The poster from the payload, the backdrop from TMDB with its download queued.
        Assert.Equal((1, 0, 1, 0), (result.ImagesFromPayload, result.ImagesFromUrl, result.ImagesPending, result.ImagesFailed));
        var poster = Assert.Single(target.LinksOf(imported.ID));
        Assert.Equal((ImageEntityType.Primary, true), (poster.ImageType, poster.IsPreferred));
        Assert.Equal(_posterFile, target.ImageStore[poster.ImageID].File);
        var backdrop = Assert.Single(target.LinksOf(imported.Seasons[0].ID));
        Assert.Equal(ImageEntityType.Backdrop, backdrop.ImageType);
        Assert.Equal(("/backdrop.jpg", "en"), (target.ImageStore[backdrop.ImageID].ResourceID, target.ImageStore[backdrop.ImageID].LanguageCode));
        Assert.Equal([backdrop.ImageID], target.Downloads);
    }

    [Theory]
    [InlineData("{ \"format\": \"something-else\", \"version\": 1, \"orderings\": [] }")]
    [InlineData("{ \"format\": \"shoko-orderings\", \"version\": 2, \"orderings\": [] }")]
    [InlineData("{ \"format\": \"shoko-orderings\", ")]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{ \"orderings\": [] }")]
    [InlineData("{ \"format\": \"shoko-orderings\", \"orderings\": [] }")]
    public async Task APayloadThatIsNotAnExportThisServerReadsChangesNothing(string payload)
    {
        var world = new World();
        AddAnime100(world);

        var result = await world.Import(Encoding.UTF8.GetBytes(payload));

        Assert.False(result.Succeeded);
        Assert.Single(result.Errors);
        Assert.Empty(world.Orderings.GetStoredOrderings(MetadataSource.User));
    }

    [Fact]
    public async Task AZipWithoutAManifestIsRefused()
    {
        var world = new World();
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            archive.CreateEntry("images/readme.txt");

        var result = await world.Import(buffer.ToArray());

        Assert.False(result.Succeeded);
        Assert.Single(result.Errors);
    }

    #endregion

    #region Networks

    [Fact]
    public async Task AnOrderingsNetworksGoByTheirIDsAndComeBackAsStubsUntilTheirSourceSavesThem()
    {
        var tokyoMX = new MetadataGuid(TestSources.Plugin, MetadataEntityType.Network, "n1");
        var fujiTV = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Network, "82");
        var source = new World();
        source.Tables.StudioStore.SaveNetworks([new() { ID = tokyoMX, Name = "Tokyo MX" }]);
        source.Orderings.CreateLocalOrdering(new() { SeriesID = AddAnime100(source).ID, Name = "Broadcast", Networks = [tokyoMX, fujiTV] });
        byte[] content;
        using (new RepoFactoryScope().Set(source.Tables.Networks))
            (content, _) = await source.Export();

        Assert.Equal(["test-plugin://network/n1", "tmdb://network/82"], Assert.Single(ReadJson(content).Orderings).Networks);

        // A dry run reports both as stubs and writes none.
        var target = new World();
        AddAnime100(target, offset: 50);
        using var scope = new RepoFactoryScope().Set(target.Tables.Networks);
        var planned = Assert.Single((await target.Import(content, new() { DryRun = true })).Orderings);
        Assert.Equal([tokyoMX, fujiTV], planned.Networks);
        Assert.Equal([tokyoMX, fujiTV], planned.StubbedNetworks);
        Assert.Empty(target.Tables.Networks.GetAll());

        var entry = Assert.Single((await target.Import(content)).Orderings);
        Assert.Equal([tokyoMX, fujiTV], entry.StubbedNetworks);
        var imported = target.Orderings.GetOrdering(entry.OrderingID!)!;
        Assert.Equal([tokyoMX, fujiTV], imported.Networks.Select(network => network.ID));
        Assert.Equal(["", ""], imported.Networks.Select(network => network.Name));

        // The provider's next save fills the stub in.
        target.Tables.StudioStore.SaveNetworks([new() { ID = tokyoMX, Name = "Tokyo MX" }]);
        Assert.Equal("Tokyo MX", target.Orderings.GetOrdering(entry.OrderingID!)!.Networks[0].Name);
    }

    [Fact]
    public void ANetworkThatIsNotOneOrIsOnASourceThisServerDoesNotKnowIsLeftOutWithANote()
    {
        var notes = new List<string>();

        var networks = MetadataOrderingTransferService.ResolveNetworks(
            new() { Networks = ["tmdb://network/82", "not an ID", "tmdb://studio/1", "not-installed-here://network/1", "tmdb://network/82"] },
            notes
        );

        Assert.Equal([new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Network, "82")], networks);
        Assert.Equal(3, notes.Count);
        Assert.Null(MetadataOrderingTransferService.ResolveNetworks(new(), notes));
    }

    #endregion

    #region ID Translation

    [Fact]
    public async Task AnOrderingOfAnotherSourceIsWrittenByTheAnidbEpisodesItsEpisodesAreLinkedTo()
    {
        var world = new World();
        var series = AddAnime100(world);
        var other = world.AddSeries(2, 200, "Anime 200", (20, 2001, EpisodeType.Episode, 1));
        var linked = world.AddLinkedSeries(
            TestSources.Plugin,
            "show",
            ("e1", [series.Episodes[0], series.Episodes[1]]),
            ("e2", [series.Episodes[2]]),
            ("e3", [other.Episodes[0]]),
            ("e4", [])
        );
        var global = world.Orderings.SaveOrdering(new()
        {
            ID = new(TestSources.Plugin, MetadataEntityType.Ordering, "arc"),
            SeriesID = linked.ID,
            Name = "Arcs",
            Type = OrderingType.StoryArc,
            Groups =
            [
                new()
                {
                    ID = new(TestSources.Plugin, MetadataEntityType.Season, "arc-1"),
                    Name = "Arc 1",
                    Episodes = [.. linked.Episodes.Select(episode => episode.ID)],
                },
            ],
        });

        var (content, result) = await world.Export(new() { OrderingIDs = [global.ID] });

        // Most of its episodes are of anime 100, and the one linked to nothing is left out.
        var written = Assert.Single(ReadJson(content).Orderings);
        Assert.Equal(100, written.Series!.AnidbAnimeId);
        Assert.Equal(OrderingType.StoryArc, written.Type);
        Assert.Equal([1001, 1002, 1003, 2001], written.Groups[0].Episodes.Select(episode => episode.AnidbEpisodeId!.Value));
        Assert.Contains(result.Warnings, warning => warning.Contains("e4") && warning.Contains("no AniDB episode"));

        // Imported, it is a local ordering of anime 100 without the other anime's episode.
        var import = await world.Import(content);

        var entry = Assert.Single(import.Orderings);
        Assert.Equal(MetadataOrderingImportOutcome.Created, entry.Outcome);
        var unresolved = Assert.Single(entry.UnresolvedEpisodes);
        Assert.Equal((0, "Arc 1", 2001), (unresolved.GroupIndex, unresolved.GroupName, unresolved.AnidbEpisodeID!.Value));
        Assert.NotNull(unresolved.Reason);
        var imported = world.Orderings.GetOrdering(entry.OrderingID!)!;
        Assert.Equal(series.ID, imported.SeriesID);
        Assert.Equal(OrderingType.User, imported.Type);
        Assert.Equal([World.Episode(10), World.Episode(11), World.Episode(12)], imported.Episodes.Select(episode => episode.ID));
    }

    [Fact]
    public async Task AnEpisodeOfAnotherAnimeIsNeverTakenForOneOfTheOrderingsAnimeByItsTypeAndNumber()
    {
        var source = new World();
        var series = AddAnime100(source);
        var other = source.AddSeries(2, 200, "Anime 200", (20, 2001, EpisodeType.Episode, 1));
        var linked = source.AddLinkedSeries(
            TestSources.Plugin,
            "show",
            ("e1", [series.Episodes[0]]),
            ("e2", [series.Episodes[1]]),
            ("e3", [other.Episodes[0]]),
            ("e4", [series.Episodes[2]])
        );
        var fork = source.Orderings.SaveOrdering(new()
        {
            ID = new(TestSources.Plugin, MetadataEntityType.Ordering, "arc"),
            SeriesID = linked.ID,
            Name = "Arcs",
            Type = OrderingType.StoryArc,
            Groups =
            [
                new() { ID = new(TestSources.Plugin, MetadataEntityType.Season, "arc-1"), Name = "Arc 1", Episodes = [linked.Episodes[0].ID, linked.Episodes[1].ID] },
                new() { ID = new(TestSources.Plugin, MetadataEntityType.Season, "arc-2"), Name = "Arc 2", Episodes = [linked.Episodes[2].ID, linked.Episodes[3].ID] },
            ],
        });
        var (content, _) = await source.Export(new() { OrderingIDs = [fork.ID] });
        Assert.Equal([100, 100], ReadJson(content).Orderings[0].Groups[0].Episodes.Select(episode => episode.AnidbAnimeId!.Value));
        Assert.Equal([200, 100], ReadJson(content).Orderings[0].Groups[1].Episodes.Select(episode => episode.AnidbAnimeId!.Value));

        // The other server has anime 100 only, so anime 200's first episode
        // must not stand in for anime 100's.
        var target = new World();
        AddAnime100(target, offset: 50);
        var result = await target.Import(content);

        var entry = Assert.Single(result.Orderings);
        Assert.Equal(MetadataOrderingImportOutcome.Created, entry.Outcome);
        var unresolved = Assert.Single(entry.UnresolvedEpisodes);
        Assert.Equal((1, "Arc 2", 2001), (unresolved.GroupIndex, unresolved.GroupName, unresolved.AnidbEpisodeID!.Value));
        Assert.NotNull(unresolved.Reason);
        var imported = target.Orderings.GetOrdering(entry.OrderingID!)!;
        Assert.Equal([World.Episode(60), World.Episode(61)], imported.Seasons[0].Episodes.Select(episode => episode.ID));
        Assert.Equal([World.Episode(62)], imported.Seasons[1].Episodes.Select(episode => episode.ID));
    }

    [Fact]
    public async Task AnEpisodeThisServerDoesNotKnowIsFoundByItsTypeAndNumberOrDropped()
    {
        var world = new World();
        AddAnime100(world);
        var group = Group("All", 1001);
        group.Episodes.Add(new() { AnidbEpisodeId = 9999, Type = EpisodeType.Special, Number = 1 });
        group.Episodes.Add(new() { AnidbEpisodeId = 9998, Type = EpisodeType.Special, Number = 7 });
        group.Episodes.Add(new() { Type = EpisodeType.Episode, Number = 3 });
        group.Episodes.Add(new() { AnidbEpisodeId = 9997 });
        group.Episodes.Add(new());
        // Naming its own anime, an episode still falls back on its type and number.
        group.Episodes.Add(new() { AnidbAnimeId = 100, AnidbEpisodeId = 9996, Type = EpisodeType.Episode, Number = 2 });

        var result = await world.Import(Json(Document(Ordering(100, "Mine", group), Ordering(300, "Elsewhere", Group("All", 3001)))));

        var entry = result.Orderings[0];
        Assert.Equal(MetadataOrderingImportOutcome.Created, entry.Outcome);
        Assert.Equal(
            [World.Episode(10), World.Episode(13), World.Episode(12), World.Episode(11)],
            world.Orderings.GetOrdering(entry.OrderingID!)!.Episodes.Select(episode => episode.ID)
        );
        Assert.Equal(new int?[] { 9998, 9997, null }, entry.UnresolvedEpisodes.Select(episode => episode.AnidbEpisodeID));
        Assert.All(entry.UnresolvedEpisodes, episode => Assert.NotNull(episode.Reason));

        // An ordering whose anime is not in the collection is skipped.
        Assert.Equal(MetadataOrderingImportOutcome.Skipped, result.Orderings[1].Outcome);
        Assert.NotNull(result.Orderings[1].Reason);
        Assert.Null(result.Orderings[1].SeriesID);
    }

    [Fact]
    public async Task AnImportKeepsOneSpecialGroupAndNamesWhatHasNoName()
    {
        var world = new World();
        AddAnime100(world);
        var first = Group("", 1004);
        first.IsSpecial = true;
        var second = Group("More", 1001, 1001);
        second.IsSpecial = true;

        var result = await world.Import(Json(Document(Ordering(100, " ", first, second))));

        var entry = Assert.Single(result.Orderings);
        Assert.Equal(MetadataOrderingTransferService.UnnamedOrdering, entry.StoredName);
        var imported = world.Orderings.GetOrdering(entry.OrderingID!)!;
        Assert.Equal(["Group 1", "More"], imported.Seasons.Select(group => group.Title));
        Assert.Equal([true, false], imported.Seasons.Select(group => group.IsSpecial));
        Assert.Equal([World.Episode(10)], imported.Seasons[1].Episodes.Select(episode => episode.ID));
    }

    #endregion

    #region Conflicts

    [Fact]
    public async Task AnOrderingOfTheSameNameIsSkippedByDefault()
    {
        var world = new World();
        var series = AddAnime100(world);
        var existing = world.Orderings.CreateLocalOrdering(new() { SeriesID = series.ID, Name = "dvd order", Groups = [new() { Name = "All", Episodes = [World.Episode(10)] }] });

        var result = await world.Import(Json(Document(Ordering(100, "DVD Order", Group("Disc 1", 1002)))));

        var entry = Assert.Single(result.Orderings);
        Assert.Equal(MetadataOrderingImportOutcome.Skipped, entry.Outcome);
        Assert.NotNull(entry.Reason);
        Assert.Equal([existing.ID], world.Orderings.GetStoredOrderings(MetadataSource.User).Select(ordering => ordering.ID));
    }

    [Fact]
    public async Task ReplacingKeepsTheOrderingsIDAndChoiceAndReplacesItsGroupsAndImages()
    {
        var world = new World();
        var series = AddAnime100(world);
        var existing = world.Orderings.CreateLocalOrdering(new() { SeriesID = series.ID, Name = "DVD Order", Groups = [new() { Name = "Old", Episodes = [World.Episode(10)] }] });
        world.Orderings.SetPreferredOrdering(series.ID, existing.ID);
        var oldPoster = world.Images.Object.UploadImage(_discFile);
        world.Link(existing, oldPoster, ImageEntityType.Primary, preferred: true);
        world.Link(existing.Seasons[0], oldPoster, ImageEntityType.Primary);
        var ordering = Ordering(100, "DVD Order", Group("New", 1003, 1002));
        ordering.Images.Add(new() { ImageType = ImageEntityType.Primary, IsPreferred = true, Data = Convert.ToBase64String(_posterFile) });

        var result = await world.Import(Json(Document(ordering)), new() { ConflictMode = MetadataOrderingConflictMode.Replace });

        var entry = Assert.Single(result.Orderings);
        Assert.Equal((MetadataOrderingImportOutcome.Replaced, existing.ID), (entry.Outcome, entry.OrderingID));
        var replaced = world.Orderings.GetOrdering(existing.ID)!;
        Assert.True(replaced.IsPreferred);
        Assert.Equal(["New"], replaced.Seasons.Select(group => group.Title));
        Assert.Equal([World.Episode(12), World.Episode(11)], replaced.Episodes.Select(episode => episode.ID));
        var poster = Assert.Single(world.LinksOf(existing.ID));
        Assert.Equal(_posterFile, world.ImageStore[poster.ImageID].File);
        Assert.Empty(world.LinksOf(existing.Seasons[0].ID));
        Assert.Single(world.Orderings.GetStoredOrderings(MetadataSource.User));
    }

    [Fact]
    public async Task KeepingBothGivesTheImportedOrderingAFreeName()
    {
        var world = new World();
        var series = AddAnime100(world);
        world.Orderings.CreateLocalOrdering(new() { SeriesID = series.ID, Name = "DVD Order", Groups = [] });
        world.Orderings.CreateLocalOrdering(new() { SeriesID = series.ID, Name = "DVD Order (2)", Groups = [] });
        var payload = Json(Document(Ordering(100, "DVD Order", Group("Disc 1", 1001)), Ordering(100, "DVD Order", Group("Disc 1", 1002))));

        var result = await world.Import(payload, new() { ConflictMode = MetadataOrderingConflictMode.KeepBoth });

        Assert.Equal(["DVD Order (3)", "DVD Order (4)"], result.Orderings.Select(entry => entry.StoredName));
        Assert.Equal(4, world.Orderings.GetStoredOrderings(MetadataSource.User).Count);
    }

    [Fact]
    public async Task ADryRunReportsWhatWouldBeDoneAndWritesNothing()
    {
        var world = new World();
        var series = AddAnime100(world);
        var existing = world.Orderings.CreateLocalOrdering(new() { SeriesID = series.ID, Name = "Kept", Groups = [] });
        var fresh = Ordering(100, "Fresh", Group("All", 1001, 9999));
        fresh.IsPreferred = true;
        fresh.Images.Add(new() { ImageType = ImageEntityType.Primary, Data = Convert.ToBase64String(_posterFile) });
        fresh.Images.Add(new() { ImageType = ImageEntityType.Backdrop, Source = "tmdb", ResourceId = "/b.jpg" });
        var payload = Json(Document(fresh, Ordering(100, "Kept", Group("All", 1001)), Ordering(100, "Fresh", Group("All", 1002))));

        var result = await world.Import(payload, new() { DryRun = true, ApplyPreferred = true });

        Assert.True(result.DryRun);
        Assert.Equal(
            [MetadataOrderingImportOutcome.Created, MetadataOrderingImportOutcome.Skipped, MetadataOrderingImportOutcome.Skipped],
            result.Orderings.Select(entry => entry.Outcome)
        );
        Assert.True(result.Orderings[0].IsPreferred);
        Assert.Single(result.Orderings[0].UnresolvedEpisodes);
        Assert.Equal(
            [MetadataOrderingImageImportStatus.FromPayload, MetadataOrderingImageImportStatus.Pending],
            result.Orderings[0].Images.Select(image => image.Status)
        );
        Assert.Equal([existing.ID], world.Orderings.GetStoredOrderings(MetadataSource.User).Select(ordering => ordering.ID));
        Assert.Empty(world.ImageStore);
        Assert.Empty(world.Downloads);
    }

    [Fact]
    public async Task ThePreferredChoiceIsOnlyAppliedWhenAskedFor()
    {
        var world = new World();
        var series = AddAnime100(world);
        var ordering = Ordering(100, "Chosen", Group("All", 1001));
        ordering.IsPreferred = true;

        var left = await world.Import(Json(Document(ordering)));
        Assert.False(left.Orderings[0].IsPreferred);
        Assert.True(world.Orderings.GetPreferredOrdering(series).IsDefault);

        ordering.Name = "Chosen again";
        var applied = await world.Import(Json(Document(ordering)), new() { ApplyPreferred = true });
        Assert.True(applied.Orderings[0].IsPreferred);
        Assert.Equal(applied.Orderings[0].OrderingID, world.Orderings.GetPreferredOrdering(series).ID);
    }

    #endregion

    #region Images

    /// <summary>
    /// An ordering whose poster has both a file in the payload and a TMDB
    /// URL.
    /// </summary>
    private static byte[] PayloadWithPoster(string? sha256 = null, bool withUrl = true, bool withFile = true)
    {
        var ordering = Ordering(100, "Posters", Group("All", 1001));
        ordering.Images.Add(new()
        {
            ImageType = ImageEntityType.Primary,
            IsPreferred = true,
            Source = withUrl ? "tmdb" : null,
            ResourceId = withUrl ? "/poster.jpg" : null,
            Url = withUrl ? "https://image.tmdb.org/t/p/original/poster.jpg" : null,
            Sha256 = sha256 ?? MetadataOrderingTransferService.Sha256(_posterFile),
            Data = withFile ? Convert.ToBase64String(_posterFile) : null,
        });
        return Json(Document(ordering));
    }

    [Theory]
    [InlineData(MetadataOrderingImageImportMode.PayloadFirst, MetadataOrderingImageImportStatus.FromPayload)]
    [InlineData(MetadataOrderingImageImportMode.PayloadOnly, MetadataOrderingImageImportStatus.FromPayload)]
    [InlineData(MetadataOrderingImageImportMode.UrlFirst, MetadataOrderingImageImportStatus.Pending)]
    [InlineData(MetadataOrderingImageImportMode.UrlOnly, MetadataOrderingImageImportStatus.Pending)]
    [InlineData(MetadataOrderingImageImportMode.None, MetadataOrderingImageImportStatus.Skipped)]
    public async Task TheImageModeChoosesWhereAnImageComesFrom(MetadataOrderingImageImportMode mode, MetadataOrderingImageImportStatus expected)
    {
        var world = new World();
        AddAnime100(world);

        var result = await world.Import(PayloadWithPoster(), new() { ImageMode = mode });

        var image = Assert.Single(result.Orderings[0].Images);
        Assert.Equal(expected, image.Status);
        if (expected is not MetadataOrderingImageImportStatus.Skipped)
            Assert.Null(image.Reason);
        var links = world.LinksOf(result.Orderings[0].OrderingID!).ToList();
        if (expected is MetadataOrderingImageImportStatus.Skipped)
        {
            Assert.Empty(links);
            return;
        }

        // Either way it is the TMDB image, so it keeps its URL; from the
        // payload it is held at once.
        var link = Assert.Single(links);
        Assert.True(link.IsPreferred);
        Assert.Equal((MetadataSource.TMDB, "/poster.jpg"), (world.ImageStore[link.ImageID].Source, world.ImageStore[link.ImageID].ResourceID));
        Assert.Equal(expected is MetadataOrderingImageImportStatus.FromPayload ? _posterFile : null, world.ImageStore[link.ImageID].File);
        Assert.DoesNotContain(world.ImageStore.Values, stored => stored.Source == MetadataSource.User);
    }

    [Fact]
    public async Task AFileOfARemoteImageAlreadyHeldIsNotWrittenAgain()
    {
        var world = new World();
        AddAnime100(world);
        var held = world.Store(new() { ID = Guid.NewGuid(), Source = MetadataSource.TMDB, ResourceID = "/poster.jpg", File = [1, 2, 3] });

        var result = await world.Import(PayloadWithPoster());

        var image = Assert.Single(result.Orderings[0].Images);
        Assert.Equal((MetadataOrderingImageImportStatus.FromPayload, held.ID), (image.Status, image.ImageID!.Value));
        world.Files.Verify(files => files.StoreFile(It.IsAny<IImage>(), It.IsAny<byte[]>()), Times.Never());
    }

    [Fact]
    public async Task AFileOfASourceThisServerCannotFetchFromBecomesAUsersImage()
    {
        var world = new World();
        AddAnime100(world);
        var ordering = Ordering(100, "Sources", Group("All", 1001));
        ordering.Images.Add(new()
        {
            ImageType = ImageEntityType.Primary,
            Source = "anidb",
            ResourceId = "1.jpg",
            Sha256 = MetadataOrderingTransferService.Sha256(_posterFile),
            Data = Convert.ToBase64String(_posterFile),
        });

        var result = await world.Import(Json(Document(ordering)));

        var image = Assert.Single(result.Orderings[0].Images);
        Assert.Equal(MetadataOrderingImageImportStatus.FromPayload, image.Status);
        Assert.Equal(MetadataSource.User, world.ImageStore[image.ImageID!.Value].Source);
        world.Files.Verify(files => files.StoreFile(It.IsAny<IImage>(), It.IsAny<byte[]>()), Times.Never());
    }

    [Fact]
    public async Task AFileTheImageManagerRefusesFailsThePayload()
    {
        var world = new World();
        AddAnime100(world);
        world.Files.Setup(files => files.StoreFile(It.IsAny<IImage>(), It.IsAny<byte[]>())).Throws(new ArgumentException("Not an image."));

        var result = await world.Import(PayloadWithPoster(), new() { ImageMode = MetadataOrderingImageImportMode.PayloadOnly });

        var image = Assert.Single(result.Orderings[0].Images);
        Assert.Equal(MetadataOrderingImageImportStatus.Failed, image.Status);
        Assert.NotNull(image.Reason);
        Assert.Empty(world.LinksOf(result.Orderings[0].OrderingID!));
    }

    [Fact]
    public async Task AFileWhoseHashDoesNotMatchIsRefusedAndTheUrlIsTriedInstead()
    {
        var world = new World();
        AddAnime100(world);
        var wrongHash = new string('0', 64);

        var fallback = await world.Import(PayloadWithPoster(wrongHash));

        var image = Assert.Single(fallback.Orderings[0].Images);
        Assert.Equal(MetadataOrderingImageImportStatus.Pending, image.Status);
        Assert.NotNull(image.Reason);
        Assert.DoesNotContain(world.ImageStore.Values, stored => stored.Source == MetadataSource.User);

        var payloadOnly = await world.Import(PayloadWithPoster(wrongHash), new() { ImageMode = MetadataOrderingImageImportMode.PayloadOnly, ConflictMode = MetadataOrderingConflictMode.KeepBoth });
        Assert.Equal(MetadataOrderingImageImportStatus.Failed, payloadOnly.Orderings[0].Images[0].Status);
        Assert.Empty(world.LinksOf(payloadOnly.Orderings[0].OrderingID!));

        // Not checked, the file is taken as it is.
        var unverified = await world.Import(PayloadWithPoster(wrongHash), new() { VerifyHashes = false, ConflictMode = MetadataOrderingConflictMode.KeepBoth });
        Assert.Equal(MetadataOrderingImageImportStatus.FromPayload, unverified.Orderings[0].Images[0].Status);
    }

    [Fact]
    public async Task AnImageWithoutAUrlCanOnlyComeFromThePayload()
    {
        var world = new World();
        AddAnime100(world);

        var urlOnly = await world.Import(PayloadWithPoster(withUrl: false), new() { ImageMode = MetadataOrderingImageImportMode.UrlOnly });
        var failed = Assert.Single(urlOnly.Orderings[0].Images);
        Assert.Equal(MetadataOrderingImageImportStatus.Failed, failed.Status);
        Assert.NotNull(failed.Reason);

        var urlFirst = await world.Import(
            PayloadWithPoster(withUrl: false),
            new() { ImageMode = MetadataOrderingImageImportMode.UrlFirst, ConflictMode = MetadataOrderingConflictMode.KeepBoth }
        );
        var restored = Assert.Single(urlFirst.Orderings[0].Images);
        Assert.Equal(MetadataOrderingImageImportStatus.FromPayload, restored.Status);
        Assert.NotNull(restored.Reason);

        var neither = await world.Import(PayloadWithPoster(withUrl: false, withFile: false), new() { ConflictMode = MetadataOrderingConflictMode.KeepBoth });
        Assert.Equal(MetadataOrderingImageImportStatus.Failed, neither.Orderings[0].Images[0].Status);
        Assert.NotNull(neither.Orderings[0].Images[0].Reason);
    }

    [Fact]
    public async Task AnImageOfASourceThisServerCannotFetchFromFailsByUrl()
    {
        var world = new World();
        AddAnime100(world);
        var ordering = Ordering(100, "Sources", Group("All", 1001));
        ordering.Images.Add(new() { ImageType = ImageEntityType.Primary, Source = "anidb", ResourceId = "1.jpg" });
        ordering.Images.Add(new() { ImageType = ImageEntityType.Backdrop, Source = "not-installed", Url = "https://image.tmdb.org/t/p/original/fits.jpg" });
        ordering.Images.Add(new() { ImageType = ImageEntityType.Logo, Url = "https://example.com/logo.png" });

        var result = await world.Import(Json(Document(ordering)), new() { ImageMode = MetadataOrderingImageImportMode.UrlOnly });

        var images = result.Orderings[0].Images;
        Assert.Equal(MetadataOrderingImageImportStatus.Failed, images[0].Status);
        // A URL that fits a known source's template is taken from that source.
        Assert.Equal(MetadataOrderingImageImportStatus.Pending, images[1].Status);
        Assert.Equal("/fits.jpg", world.ImageStore[images[1].ImageID!.Value].ResourceID);
        Assert.Equal(MetadataOrderingImageImportStatus.Failed, images[2].Status);
    }

    [Fact]
    public async Task AnImageAlreadyHeldIsLinkedWithoutADownload()
    {
        var world = new World();
        AddAnime100(world);
        var held = world.Store(new() { ID = Guid.NewGuid(), Source = MetadataSource.TMDB, ResourceID = "/poster.jpg", File = _posterFile });

        var result = await world.Import(PayloadWithPoster(), new() { ImageMode = MetadataOrderingImageImportMode.UrlFirst });

        var image = Assert.Single(result.Orderings[0].Images);
        Assert.Equal((MetadataOrderingImageImportStatus.FromUrl, held.ID), (image.Status, image.ImageID!.Value));
        Assert.Empty(world.Downloads);
    }

    [Theory]
    [InlineData("https://image.tmdb.org/t/p/original{0}", "https://image.tmdb.org/t/p/original/a.jpg", "/a.jpg")]
    [InlineData("https://cdn.example.com/{0}?size=large", "https://cdn.example.com/x/y.png?size=large", "x/y.png")]
    [InlineData("https://cdn.example.com/{0}?size=large", "https://cdn.example.com/x/y.png", null)]
    [InlineData("https://cdn.example.com/{0}", "https://other.example.com/x.png", null)]
    [InlineData("https://cdn.example.com/", "https://cdn.example.com/x.png", null)]
    public void AUrlIsReadBackIntoTheResourceIDOfTheTemplateItFits(string template, string url, string? expected)
        => Assert.Equal(expected, MetadataOrderingTransferService.ResourceIDOf(template, url));

    #endregion
}
