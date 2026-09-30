using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.HTTP;
using Shoko.Server.Providers.AniDB.HTTP.GetAnime;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Providers.AniDB;

/// <summary>
/// Covers the AniDB resources: <see cref="HttpAnimeParser"/> keeping every
/// external entity of every type on the anime and its episodes,
/// <see cref="AnimeCreator.DiffResources"/> replacing the stored rows, and
/// <see cref="AnidbResourceLinks"/> and the models turning the rows into links.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class AnidbResourceTests
{
    #region Helpers

    private static ResponseGetAnime Parse(int animeID, string xml)
        => new HttpAnimeParser(NullLogger<HttpAnimeParser>.Instance).Parse(animeID, xml) ?? throw new InvalidOperationException("Parse returned null.");

    private static AniDB_Resource Row(int id, ResponseResource resource)
        => new()
        {
            AniDB_ResourceID = id,
            AnimeID = resource.AnimeID,
            EpisodeID = resource.EpisodeID,
            ResourceType = resource.ResourceType,
            Ordering = resource.Ordering,
            Identifiers = [.. resource.Identifiers],
            Urls = [.. resource.Urls],
        };

    private static List<AniDB_Resource> Rows(ResponseGetAnime response)
        => [.. response.Resources.Concat(response.Episodes.SelectMany(episode => episode.Resources)).Select((resource, index) => Row(index + 1, resource))];

    private static Resource Link(ResourceLinkType type, int? episodeID = null, string[]? identifiers = null, string[]? urls = null)
        => AnidbResourceLinks.ToResource(new() { AnimeID = 1, EpisodeID = episodeID, ResourceType = type, Identifiers = [.. identifiers ?? []], Urls = [.. urls ?? []] });

    #endregion

    #region Parser

    [Fact]
    public void EveryEntityOfTheAnimeIsKeptInOrderWithSeveralOfOneType()
    {
        var response = Parse(16984, AnidbResourceFixtures.Anime16984);

        Assert.Equal(19, response.Resources.Count);
        Assert.Equal(Enumerable.Range(0, 19), response.Resources.Select(resource => resource.Ordering));
        Assert.All(response.Resources, resource => Assert.Equal((16984, (int?)null), (resource.AnimeID, resource.EpisodeID)));
        Assert.Equal(
            ["https://soushokudragon.jp/", "https://weibo.com/u/7723662904"],
            response.Resources.Where(resource => resource.ResourceType is ResourceLinkType.Site_JP).SelectMany(resource => resource.Urls)
        );
        Assert.Equal(
            ["https://www.bilibili.tv/en/media/2072897/", "https://www.bilibili.tv/en/media/1063156"],
            response.Resources.Where(resource => resource.ResourceType is ResourceLinkType.OfficialStream).SelectMany(resource => resource.Urls)
        );
        var tmdb = Assert.Single(response.Resources, resource => resource.ResourceType is ResourceLinkType.TMDB);
        Assert.Equal(["139161", "tv"], tmdb.Identifiers);
        Assert.Empty(tmdb.Urls);
        Assert.Equal(["齢5000年の草食ドラゴン、いわれなき邪竜認定"], Assert.Single(response.Resources, resource => resource.ResourceType is ResourceLinkType.Wiki_JP).Identifiers);
    }

    [Fact]
    public void EveryEntityOfAnEpisodeIsKeptOnTheEpisode()
    {
        var response = Parse(16984, AnidbResourceFixtures.Anime16984);

        var first = response.Episodes.Single(episode => episode.EpisodeID is 257718);
        Assert.Equal(
            [(257718, ResourceLinkType.Crunchyroll, 0, "G2XU0Q9EN"), (257718, ResourceLinkType.Crunchyroll, 1, "G8WUN89G9")],
            first.Resources.Select(resource => (resource.EpisodeID!.Value, resource.ResourceType, resource.Ordering, Assert.Single(resource.Identifiers)))
        );
        Assert.All(first.Resources, resource => Assert.Equal(16984, resource.AnimeID));
        Assert.Equal(["G8WUNM5Z5", "GZ7UV8N27"], response.Episodes.Single(episode => episode.EpisodeID is 257719).Resources.SelectMany(resource => resource.Identifiers));
        Assert.DoesNotContain(response.Resources, resource => resource.Identifiers.Contains("G2XU0Q9EN"));
    }

    [Fact]
    public void TypesShokoDoesNotKnowAreKeptWithTheirRawValues()
    {
        var response = Parse(4459, AnidbResourceFixtures.Anime4459);

        Assert.Equal(
            [ResourceLinkType.MAL, ResourceLinkType.MAL, ResourceLinkType.AnimeNfo, (ResourceLinkType)31, (ResourceLinkType)35, ResourceLinkType.IMDb, ResourceLinkType.TMDB],
            response.Resources.Select(resource => resource.ResourceType)
        );
        Assert.Equal(["12339"], response.Resources[3].Identifiers);
        Assert.Equal(["https://chinesedora.com/database/animation/doraemon"], response.Resources[4].Urls);
        Assert.Equal(["3734", "uphzlk"], response.Resources[2].Identifiers);
        Assert.Empty(Assert.Single(response.Episodes).Resources);
    }

    [Fact]
    public void AResourceWithoutANumericTypeOrAnEntityWithoutValuesIsSkipped()
    {
        var xml = AnidbResourceFixtures.Anime4459.Replace(
            "<resource type=\"31\">",
            "<resource type=\"x\"><externalentity><identifier>1</identifier></externalentity></resource><resource type=\"16\"><externalentity /></resource><resource type=\"31\">"
        );

        var response = Parse(4459, xml);

        Assert.Equal(7, response.Resources.Count);
        Assert.DoesNotContain(response.Resources, resource => resource.ResourceType is (ResourceLinkType)16);
    }

    #endregion

    #region Storing

    [Fact]
    public void AFirstImportSavesARowPerEntity()
    {
        var response = Parse(16984, AnidbResourceFixtures.Anime16984);

        var (toSave, toDelete) = AnimeCreator.DiffResources([], [.. response.Resources, .. response.Episodes.SelectMany(episode => episode.Resources)], 16984);

        Assert.Equal(23, toSave.Count);
        Assert.Empty(toDelete);
        Assert.Equal(4, toSave.Count(row => row.EpisodeID is not null));
    }

    [Fact]
    public void AnUnchangedImportTouchesNothingAndAChangedOneReplacesOnlyWhatChanged()
    {
        var response = Parse(16984, AnidbResourceFixtures.Anime16984);
        var stored = Rows(response);
        var wanted = response.Resources.Concat(response.Episodes.SelectMany(episode => episode.Resources)).ToList();

        var (unchangedSave, unchangedDelete) = AnimeCreator.DiffResources(stored, wanted, 16984);
        Assert.Empty(unchangedSave);
        Assert.Empty(unchangedDelete);

        // AniDB dropped the second stream and changed an episode's Crunchyroll ID.
        var trimmed = AnidbResourceFixtures.Anime16984
            .Replace("<url>https://www.bilibili.tv/en/media/1063156</url>", string.Empty)
            .Replace("<identifier>G8WUNM5Z5</identifier>", "<identifier>G8WUNM5Z6</identifier>");
        var changed = Parse(16984, trimmed);
        var (toSave, toDelete) = AnimeCreator.DiffResources(stored, [.. changed.Resources, .. changed.Episodes.SelectMany(episode => episode.Resources)], 16984);

        Assert.Equal(["G8WUNM5Z6"], toSave.Where(row => row.EpisodeID is 257719).SelectMany(row => row.Identifiers));
        Assert.All(toSave, row => Assert.NotEqual(0, row.AniDB_ResourceID));
        var removed = Assert.Single(toDelete);
        Assert.Null(removed.EpisodeID);
        Assert.Equal(18, removed.Ordering);
    }

    #endregion

    #region Links

    [Fact]
    public void KnownTypesBuildTheirPagesFromTheirIdentifiers()
    {
        Assert.Equal(("VNDB", "https://vndb.org/v11753", "v11753"), Parts(Link(ResourceLinkType.VNDB, identifiers: ["11753", "v"])));
        Assert.Equal(("TMDB", "https://www.themoviedb.org/movie/357405", "357405"), Parts(Link(ResourceLinkType.TMDB, identifiers: ["357405", "movie"])));
        Assert.Equal((".lain", "https://lain.gr.jp/mediadb/media/270", "270"), Parts(Link(ResourceLinkType.DotLain, identifiers: ["mediadb/media/270"])));

        var funimation = Link(ResourceLinkType.Funimation, identifiers: ["gunslinger-girl/"]);
        Assert.Equal((ResourceType.Streaming, "Funimation", "https://www.funimation.com/shows/gunslinger-girl/", "gunslinger-girl"),
            (funimation.Type, funimation.Name, funimation.Url, funimation.ID));

        var stream = Link(ResourceLinkType.OfficialStream, urls: ["https://www.bilibili.tv/en/media/1063156"]);
        Assert.Equal((ResourceType.Streaming, "Official Stream", "https://www.bilibili.tv/en/media/1063156"), (stream.Type, stream.Name, stream.Url));
    }

    [Theory]
    [InlineData("Sword_Art_Online#Anime", "https://en.wikipedia.org/wiki/Sword_Art_Online#Anime")]
    [InlineData("Is_the_Order_a_Rabbit%3F", "https://en.wikipedia.org/wiki/Is_the_Order_a_Rabbit%3F")]
    [InlineData("Fate/Zero", "https://en.wikipedia.org/wiki/Fate/Zero")]
    [InlineData("ガルパン#.E5.8A.87", "https://en.wikipedia.org/wiki/%E3%82%AC%E3%83%AB%E3%83%91%E3%83%B3#.E5.8A.87")]
    public void WikipediaLinksKeepAnchorsAndDoNotEncodeTwice(string title, string expected)
        => Assert.Equal(expected, Link(ResourceLinkType.Wiki_EN, identifiers: [title]).Url);

    [Fact]
    public void CrunchyrollLinksTheSeriesOnAnAnimeAndTheEpisodeOnAnEpisode()
    {
        Assert.Equal("https://www.crunchyroll.com/series/G1XHJV2NJ", Link(ResourceLinkType.Crunchyroll, identifiers: ["G1XHJV2NJ"]).Url);
        Assert.Equal("https://www.crunchyroll.com/watch/G2XU0Q9EN", Link(ResourceLinkType.Crunchyroll, 257718, ["G2XU0Q9EN"]).Url);
    }

    [Fact]
    public void UnknownTypesShowTheirRawLinkOrIdentifiers()
    {
        var identifier = Link((ResourceLinkType)31, identifiers: ["12339"]);
        Assert.Equal((ResourceType.Other, "AniDB Resource (31)", string.Empty, "12339"), (identifier.Type, identifier.Name, identifier.Url, identifier.ID));

        var url = Link((ResourceLinkType)35, urls: ["https://chinesedora.com/database/animation/doraemon"]);
        Assert.Equal((ResourceType.Other, "https://chinesedora.com/database/animation/doraemon"), (url.Type, url.Url));
        Assert.Null(url.ID);

        // A known type missing the value its page needs falls back the same way.
        Assert.Equal(ResourceType.Other, Link(ResourceLinkType.TMDB, identifiers: ["1"]).Type);
    }

    [Fact]
    public void TheModelsListEveryStoredResourceAndKeepTheirMyAnimeListLinks()
    {
        var response = Parse(4459, AnidbResourceFixtures.Anime4459);
        var episodeResponse = Parse(16984, AnidbResourceFixtures.Anime16984);
        using var scope = new RepoFactoryScope()
            .With<AniDB_ResourceRepository, int, AniDB_Resource>(row => row.AniDB_ResourceID, [.. Rows(response), .. Rows(episodeResponse).Select(row => { row.AniDB_ResourceID += 100; return row; })])
            .With<CrossRef_AniDB_MALRepository, int, CrossRef_AniDB_MAL>(xref => xref.CrossRef_AniDB_MALID,
            [
                new() { CrossRef_AniDB_MALID = 1, AnimeID = 4459, MALID = 1200 },
                new() { CrossRef_AniDB_MALID = 2, AnimeID = 4459, MALID = 999 },
            ]);

        var links = new AniDB_Anime { AnimeID = 4459 }.GetAnidbResources();

        Assert.Equal(
            ["MyAnimeList", "MyAnimeList", "AnimeNfo", "AniDB Resource (31)", "AniDB Resource (35)", "IMDb", "TMDB", "MyAnimeList"],
            links.Select(link => link.Name)
        );
        Assert.Equal(["1200", "10270", "999"], links.Where(link => link.Name is "MyAnimeList").Select(link => link.ID));
        Assert.Equal(
            ["https://www.crunchyroll.com/watch/G8WUNM5Z5", "https://www.crunchyroll.com/watch/GZ7UV8N27"],
            new AniDB_Episode { EpisodeID = 257719, AnimeID = 16984 }.GetAnidbResources().Select(link => link.Url)
        );
        Assert.Empty(new AniDB_Episode { EpisodeID = 49323, AnimeID = 4459 }.GetAnidbResources());
    }

    private static (string Name, string Url, string? ID) Parts(Resource resource)
        => (resource.Name, resource.Url, resource.ID);

    #endregion
}
