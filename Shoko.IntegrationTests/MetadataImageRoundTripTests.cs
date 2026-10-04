using System;
using System.Linq;
using System.Threading.Tasks;
using ImageMagick;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling.Jobs.Metadata;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Runs the plugin image flow against the migrated database: the reconciler
/// linking a stored series' images and unlinking dropped ones, the purge job
/// taking the links away, and an entry's own preferred image read back.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class MetadataImageRoundTripTests(DatabaseMigrationFixture fixture)
{
    private static readonly MetadataSource _plugin = TestSources.Image;

    private const string Template = "https://images.example.com/{0}";

    private static MetadataGuid ID(MetadataEntityType entityType, string id)
        => new(_plugin, entityType, id);

    /// <summary>
    /// Throws the image caches away and reads them again from the database.
    /// </summary>
    private void Reload()
    {
        fixture.Services.GetRequiredService<ShokoImageRepository>().Populate(displayName: false);
        fixture.Services.GetRequiredService<ShokoImage_EntityRepository>().Populate(displayName: false);
    }

    [Fact]
    public async Task AStoredSeriesImagesAreLinkedReadBackAndPurged()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var services = fixture.Services;
        var images = services.GetRequiredService<IImageManager>();
        var seriesStore = services.GetRequiredService<IMetadataSeriesStore>();
        var reconciler = services.GetRequiredService<MetadataImageReconciler>();
        images.RegisterTemplateUrl(_plugin, Template);
        var seriesID = ID(MetadataEntityType.Series, "image-series");
        seriesStore.SaveSeries(new() { ID = seriesID, OriginalLanguageCode = "ja" });
        var settings = new MetadataImageSettings { MaxAutoPosters = 1, InternalImageLanguageOrder = ["x-main"] };

        var linked = await reconciler.Reconcile(seriesStore.GetSeries(seriesID)!, _plugin, [
            new() { ResourceID = "posters/en.jpg", ImageType = ImageEntityType.Primary, LanguageCode = "en", Width = 600, Height = 900 },
            new() { ResourceID = "posters/ja.jpg", ImageType = ImageEntityType.Primary, LanguageCode = "ja", Rating = 8, RatingVotes = 12 },
            new() { ResourceID = "banners/a.jpg", ImageType = ImageEntityType.Banner },
        ], settings, "ja", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(3, linked);
        Reload();
        var series = seriesStore.GetSeries(seriesID)!;
        var xrefs = images.GetImageCrossReferencesForEntity(series, new() { ImageSource = _plugin, XrefSource = _plugin, LinkedEntityImages = false })
            .ToDictionary(xref => images.GetImageByID(xref.ImageID)!.ResourceID);
        Assert.Equal(["banners/a.jpg", "posters/en.jpg", "posters/ja.jpg"], xrefs.Keys.Order());
        Assert.Equal(8, xrefs["posters/ja.jpg"].Rating);
        var poster = images.GetImageBySourceAndRemoteResourceID(_plugin, "posters/en.jpg")!;
        Assert.Equal((600, 900), (poster.Width, poster.Height));
        Assert.Equal("en", poster.LanguageCode);

        await reconciler.Reconcile(series, _plugin, [
            new() { ResourceID = "posters/en.jpg", ImageType = ImageEntityType.Primary, LanguageCode = "en" },
        ], settings, "ja", cancellationToken: TestContext.Current.CancellationToken);
        Reload();

        var kept = Assert.Single(images.GetImageCrossReferencesForEntity(series, new() { ImageSource = _plugin, XrefSource = _plugin, LinkedEntityImages = false }));
        Assert.Equal(images.GetImageBySourceAndRemoteResourceID(_plugin, "posters/en.jpg")!.ID, kept.ImageID);

        using (var scope = services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<PurgeMetadataJob>();
            job.Setup(scope.ServiceProvider);
            job.EntryID = seriesID.ToString();
            await job.Execute();
        }

        Reload();
        Assert.DoesNotContain(services.GetRequiredService<ShokoImage_EntityRepository>().GetAll(), xref => xref.EntitySource == _plugin);
    }

    [Fact]
    public async Task AnEntitysOwnPreferredImageIsReadBackAsPreferred()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var services = fixture.Services;
        var images = services.GetRequiredService<IImageManager>();
        var seriesStore = services.GetRequiredService<IMetadataSeriesStore>();
        var seriesID = ID(MetadataEntityType.Series, "preferred-series");
        seriesStore.SaveSeries(new() { ID = seriesID, OriginalLanguageCode = "ja" });
        using var png = new MagickImage(MagickColors.Crimson, 8, 8) { Format = MagickFormat.Png };
        var upload = images.UploadImage(png.ToByteArray(), "image/png", MetadataSource.User);

        IWithImages series = seriesStore.GetSeries(seriesID)!;
        images.SetPreferredImageForEntity(series, ImageEntityType.Primary, upload);
        Reload();
        series = seriesStore.GetSeries(seriesID)!;

        var best = series.GetBestImageForType(ImageEntityType.Primary);
        Assert.NotNull(best);
        Assert.Equal(upload.ID, best.ID);
        Assert.True(best.IsPreferred);

        Assert.True(await images.PurgeImage(upload));
    }
}
