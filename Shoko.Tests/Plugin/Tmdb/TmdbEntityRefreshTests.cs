using System;
using System.Linq;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Plugin.Tmdb.Mapping;
using Xunit;

namespace Shoko.Tests.Plugin.Tmdb;

/// <summary>
///   The routed refresh of TMDB's people, companies and networks, and the
///   images handed out for them and for the shows.
/// </summary>
public sealed class TmdbEntityRefreshTests : IDisposable
{
    private readonly TmdbServiceHarness _harness = new();

    public void Dispose()
        => _harness.Dispose();

    #region Refresh

    [Fact]
    public async Task APersonIsWrittenFromTheirOwnRecord()
    {
        _harness.Routes.Fixture("person/5001", "person-5001.json");

        Assert.True(await _harness.Provider.RefreshEntity(TmdbIds.Creator(5001), TestContext.Current.CancellationToken));

        var creator = _harness.StoreData.Creators[TmdbIds.Creator(5001)];
        Assert.Equal("Atsumi Tanezaki", creator.Name);
        Assert.Equal("A Japanese voice actress.", creator.Overview);
        Assert.Equal(PersonGender.Female, creator.Gender);
        Assert.Equal(new FuzzyDateOnly(new DateOnly(1989, 9, 27)), creator.BirthDay);
        Assert.Null(creator.DeathDay);
        Assert.Equal("Ōita Prefecture, Japan", creator.PlaceOfBirth);
        Assert.False(creator.IsRestricted);
        Assert.Equal(["種崎敦美", "Tanezaki Atsumi"], creator.AlternativeNames.Select(name => name.Name));
        Assert.Equal(["https://example.com/tanezaki", "https://www.imdb.com/name/nm5001/"], creator.Resources.Select(resource => resource.Url));
        Assert.Equal([(ImageEntityType.Primary, "p-5001.jpg")], creator.DefaultImageResourceIDs!.Select(pair => (pair.Key, pair.Value)));

        // The English biography is the person's own and is not stored again.
        Assert.DoesNotContain(_harness.StoreData.SetOverviews[TmdbIds.Creator(5001)], overview => overview.LanguageCode is "en");
    }

    [Fact]
    public async Task ACompanyAndANetworkAreWritten()
    {
        _harness.Routes.Fixture("company/21444", "company-21444.json").Fixture("network/98", "network-98.json");

        Assert.True(await _harness.Provider.RefreshEntity(TmdbIds.Studio(21444), TestContext.Current.CancellationToken));
        Assert.True(await _harness.Provider.RefreshEntity(TmdbIds.Network(98), TestContext.Current.CancellationToken));

        Assert.Equal(("MADHOUSE Inc.", "JP"), (_harness.StoreData.Studios[TmdbIds.Studio(21444)].Name, _harness.StoreData.Studios[TmdbIds.Studio(21444)].CountryOfOrigin));
        Assert.Equal("Nippon Television", _harness.StoreData.Networks[TmdbIds.Network(98)].Name);
        Assert.Equal([(ImageEntityType.Primary, "company-21444.png")], _harness.StoreData.Studios[TmdbIds.Studio(21444)].DefaultImageResourceIDs!.Select(pair => (pair.Key, pair.Value)));
        // A network's own record names no logo, so its default is left alone.
        Assert.Null(_harness.StoreData.Networks[TmdbIds.Network(98)].DefaultImageResourceIDs);
    }

    [Fact]
    public async Task AnEntryTmdbLacksIsNotFound()
    {
        Assert.False(await _harness.Provider.RefreshEntity(TmdbIds.Creator(404), TestContext.Current.CancellationToken));
        Assert.False(await _harness.Provider.RefreshEntity(new(MetadataSource.TMDB, MetadataEntityType.Character, "1"), TestContext.Current.CancellationToken));
        Assert.Empty(_harness.StoreData.Creators);
    }

    #endregion

    #region Images

    [Fact]
    public async Task APersonsPhotosComeFromTheirRefresh()
    {
        _harness.Routes.Fixture("person/5001", "person-5001.json");
        await _harness.Provider.RefreshEntity(TmdbIds.Creator(5001), TestContext.Current.CancellationToken);

        var images = await _harness.Provider.GetImages(TmdbIds.Creator(5001), TestContext.Current.CancellationToken);

        Assert.Equal(["p-5001.jpg", "p-5001-b.jpg"], images!.Select(image => image.ResourceID));
        Assert.All(images!, image => Assert.Equal(ImageEntityType.Primary, image.ImageType));
        Assert.Equal(0, _harness.Routes.Count("person/5001/images"));
    }

    [Fact]
    public async Task AShowsImagesAreItsPostersLogosAndBackdropsOnceStored()
    {
        _harness.RouteShow().Routes.Fixture("tv/1001/images", "show-1001-images.json");
        Assert.Null(await _harness.Provider.GetImages(TmdbIds.Series(1001), TestContext.Current.CancellationToken));

        await _harness.Refresh.RefreshShow(1001, new() { QuickRefresh = true }, TestContext.Current.CancellationToken);
        var images = await _harness.Provider.GetImages(TmdbIds.Series(1001), TestContext.Current.CancellationToken);

        Assert.Equal(
            [("show-poster.jpg", ImageEntityType.Primary), ("show-logo.png", ImageEntityType.Logo), ("show-backdrop.jpg", ImageEntityType.Backdrop)],
            images!.Select(image => (image.ResourceID, image.ImageType))
        );
        Assert.Equal(("ja", 5.3, 2), (images![0].LanguageCode, images[0].Rating, images[0].RatingVotes));
    }

    [Fact]
    public async Task AnEpisodesStillsAreAskedForByItsNumbers()
    {
        _harness.RouteShow().Routes.Fixture("tv/1001/season/1/episode/1/images", "episode-1001-1-1-images.json");
        await _harness.Refresh.RefreshShow(1001, new() { QuickRefresh = true }, TestContext.Current.CancellationToken);

        var images = await _harness.Provider.GetImages(TmdbIds.Episode(3001), TestContext.Current.CancellationToken);

        Assert.Equal([("still-3001.jpg", ImageEntityType.Backdrop)], images!.Select(image => (image.ResourceID, image.ImageType)));
    }

    [Fact]
    public async Task ANetworksSvgLogoIsAskedForAsAPng()
    {
        _harness.Routes.Fixture("network/98/images", "network-98-images.json");

        var images = await _harness.Provider.GetImages(TmdbIds.Network(98), TestContext.Current.CancellationToken);

        Assert.Equal(["network-98.png"], images!.Select(image => image.ResourceID));
    }

    [Fact]
    public async Task ACompanysLogoIsItsOnlyImage()
    {
        _harness.Routes.Fixture("company/21444", "company-21444.json");

        var image = Assert.Single((await _harness.Provider.GetImages(TmdbIds.Studio(21444), TestContext.Current.CancellationToken))!);

        Assert.Equal(("company-21444.png", ImageEntityType.Primary), (image.ResourceID, image.ImageType));
    }

    #endregion
}
