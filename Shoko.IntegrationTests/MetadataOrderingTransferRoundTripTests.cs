using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using ImageMagick;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Orderings;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Services;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Exports a local ordering with images on it and its groups into a zip,
/// wipes the ordering and the uploaded images, imports the zip and reads
/// everything back from the migrated database.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class MetadataOrderingTransferRoundTripTests(DatabaseMigrationFixture fixture)
{
    private const int AnimeID = 988_101;

    private const string Template = "https://images.example.com/{0}";

    /// <summary>
    /// Throws the ordering and image caches away and reads them again from
    /// the database.
    /// </summary>
    private void Reload()
    {
        var services = fixture.Services;
        foreach (var repository in new ICachedRepository[]
        {
            services.GetRequiredService<Metadata_OrderingRepository>(),
            services.GetRequiredService<Metadata_Ordering_GroupRepository>(),
            services.GetRequiredService<Metadata_Ordering_EntryRepository>(),
            services.GetRequiredService<ShokoImageRepository>(),
            services.GetRequiredService<ShokoImage_EntityRepository>(),
        })
            repository.Populate(displayName: false);
    }

    /// <summary>
    /// A small PNG of one colour.
    /// </summary>
    private static byte[] Png(MagickColor color)
    {
        using var image = new MagickImage(color, 8, 8) { Format = MagickFormat.Png };
        return image.ToByteArray();
    }

    /// <summary>
    /// The bytes of an image as held on disk.
    /// </summary>
    private static byte[] Held(IImage image)
    {
        using var stream = image.GetStream();
        Assert.NotNull(stream);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    [Fact]
    public async Task AnOrderingWithGroupImagesComesBackWholeFromAZip()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var services = fixture.Services;
        var cancellationToken = TestContext.Current.CancellationToken;
        var orderings = services.GetRequiredService<IMetadataOrderingService>();
        var images = services.GetRequiredService<IImageManager>();
        var transfer = services.GetRequiredService<IMetadataOrderingTransferService>();
        images.RegisterTemplateUrl(TestSources.Image, Template);

        // An anime in the collection with three episodes and a special.
        var anime = services.GetRequiredService<AniDB_AnimeRepository>();
        var anidbEpisodes = services.GetRequiredService<AniDB_EpisodeRepository>();
        var shokoEpisodes = services.GetRequiredService<AnimeEpisodeRepository>();
        anime.Save(new AniDB_Anime { AnimeID = AnimeID, MainTitle = "Transferred" });
        var episodeTypes = new[] { EpisodeType.Episode, EpisodeType.Episode, EpisodeType.Episode, EpisodeType.Special };
        for (var index = 0; index < episodeTypes.Length; index++)
            anidbEpisodes.Save(new AniDB_Episode
            {
                EpisodeID = AnimeID + 1 + index,
                AnimeID = AnimeID,
                EpisodeNumber = episodeTypes[index] is EpisodeType.Special ? 1 : index + 1,
                EpisodeType = episodeTypes[index],
            });
        var group = new AnimeGroup { DateTimeCreated = DateTime.Now, DateTimeUpdated = DateTime.Now };
        services.GetRequiredService<AnimeGroupRepository>().Save(group, false);
        var shokoSeries = new AnimeSeries { AniDB_ID = AnimeID, AnimeGroupID = group.AnimeGroupID, DateTimeCreated = DateTime.Now, DateTimeUpdated = DateTime.Now, UpdatedAt = DateTime.Now };
        services.GetRequiredService<AnimeSeriesRepository>().Save(shokoSeries, updateGroups: false, alsoupdateepisodes: false);
        var episodes = Enumerable.Range(0, episodeTypes.Length).Select(index =>
        {
            var episode = new AnimeEpisode { AnimeSeriesID = shokoSeries.AnimeSeriesID, AniDB_EpisodeID = AnimeID + 1 + index, DateTimeCreated = DateTime.Now, DateTimeUpdated = DateTime.Now };
            shokoEpisodes.Save(episode);
            return new MetadataGuid(MetadataSource.Shoko, MetadataEntityType.Episode, episode.AnimeEpisodeID.ToString());
        }).ToList();
        var seriesID = new MetadataGuid(MetadataSource.Shoko, MetadataEntityType.Series, shokoSeries.AnimeSeriesID.ToString());

        var ordering = orderings.CreateLocalOrdering(new()
        {
            SeriesID = seriesID,
            Titles = MetadataOrderingService.UserTitles("Transfer Order"),
            Overviews = MetadataOrderingService.UserOverviews("Moved between servers."),
            Groups =
            [
                new()
                {
                    Titles = MetadataOrderingService.UserTitles("Part 1"),
                    Overviews = MetadataOrderingService.UserOverviews("The start."),
                    Episodes = [episodes[1], episodes[0]],
                },
                new() { Titles = MetadataOrderingService.UserTitles("Extras"), IsSpecial = true, Episodes = [episodes[3]] },
                new() { Titles = MetadataOrderingService.UserTitles("Part 2"), Episodes = [episodes[2]] },
            ],
        });
        Assert.True(orderings.SetPreferredOrdering(seriesID, ordering.ID));

        // An uploaded poster on the ordering, an uploaded backdrop on its first
        // group, and a logo of a remote source on its last.
        var posterFile = Png(MagickColors.Red);
        var backdropFile = Png(MagickColors.Blue);
        var poster = images.UploadImage(posterFile);
        var backdrop = images.UploadImage(backdropFile);
        var logo = images.GetImageBySourceAndRemoteResourceID(TestSources.Image, "orderings/logo.png")
            ?? images.AddImage(new() { Source = TestSources.Image, ResourceID = "orderings/logo.png", LanguageCode = "ja" });
        images.SetPreferredImageForEntity(ordering, ImageEntityType.Primary, poster);
        images.AddImageCrossReference(ordering.Seasons[0], backdrop, new() { ImageType = ImageEntityType.Backdrop, IsEnabled = true });
        images.AddImageCrossReference(ordering.Seasons[2], logo, new() { ImageType = ImageEntityType.Logo, IsEnabled = true });
        Reload();

        var read = orderings.GetOrdering(ordering.ID)!;
        Assert.Equal([poster.ID], ((IWithImages)read).GetImages().Select(image => image.ID));
        Assert.Equal([backdrop.ID], ((IWithImages)read.Seasons[0]).GetImages().Select(image => image.ID));

        var export = await transfer.ExportToBytes(
            new() { SeriesIDs = [seriesID], ImageMode = MetadataOrderingImageExportMode.EmbedMissingRemote },
            cancellationToken
        );
        Assert.Equal(MetadataOrderingContainer.Zip, export.Result.Container);

        Assert.True(orderings.DeleteLocalOrdering(ordering.ID));
        foreach (var uploaded in new[] { poster, backdrop })
        {
            var path = uploaded.LocalPath;
            Assert.True(await images.PurgeImage(uploaded));
            Assert.False(File.Exists(path));
        }

        Reload();
        Assert.Null(images.GetImageByID(poster.ID));

        var result = await transfer.Import(new MemoryStream(export.Content), new() { ApplyPreferred = true }, cancellationToken);

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        var entry = Assert.Single(result.Orderings);
        Assert.Equal(MetadataOrderingImportOutcome.Created, entry.Outcome);
        Assert.Empty(entry.UnresolvedEpisodes);
        Assert.True(entry.IsPreferred);
        Assert.Equal((2, 1), (result.ImagesFromPayload, result.ImagesPending + result.ImagesFromUrl));

        Reload();

        var imported = orderings.GetOrdering(entry.OrderingID!)!;
        Assert.Equal(("Transfer Order", "Moved between servers.", OrderingType.User), (imported.Title, imported.DefaultOverview?.Value, imported.Type));
        Assert.True(imported.IsPreferred);
        Assert.Equal(["Part 1", "Extras", "Part 2"], imported.Seasons.Select(season => season.Title));
        Assert.Equal([false, true, false], imported.Seasons.Select(season => season.IsSpecial));
        Assert.Equal("The start.", imported.Seasons[0].DefaultOverview?.Value);
        Assert.Equal([episodes[1], episodes[0]], imported.Seasons[0].Episodes.Select(episode => episode.ID));
        Assert.Equal([episodes[3]], imported.Seasons[1].Episodes.Select(episode => episode.ID));
        Assert.Equal([episodes[2]], imported.Seasons[2].Episodes.Select(episode => episode.ID));

        var importedPoster = Assert.Single(((IWithImages)imported).GetImages());
        Assert.Equal((ImageEntityType.Primary, true), (importedPoster.Type, importedPoster.IsPreferred));
        Assert.Equal(SHA256.HashData(posterFile), SHA256.HashData(Held(importedPoster)));
        var importedBackdrop = Assert.Single(((IWithImages)imported.Seasons[0]).GetImages());
        Assert.Equal(ImageEntityType.Backdrop, importedBackdrop.Type);
        Assert.Equal(backdropFile, Held(importedBackdrop));
        var importedLogo = Assert.Single(((IWithImages)imported.Seasons[2]).GetImages());
        Assert.Equal((logo.ID, ImageEntityType.Logo), (importedLogo.ID, importedLogo.Type));

        // Nothing is left for the other tests sharing the database.
        Assert.True(orderings.DeleteLocalOrdering(imported.ID));
        foreach (var image in new[] { importedPoster, importedBackdrop, importedLogo })
            await images.PurgeImage(image);
        await services.GetRequiredService<IAnidbService>().PurgeAnimeByID(AnimeID, removeFromMylist: false);
        Reload();
        Assert.DoesNotContain(orderings.GetStoredOrderings(MetadataSource.User), stored => stored.SeriesID == seriesID);
    }
}
