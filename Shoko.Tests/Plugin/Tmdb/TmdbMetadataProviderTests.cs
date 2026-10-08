using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Matching;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Plugin;
using Shoko.Plugin.Tmdb.Mapping;
using Shoko.Plugin.Tmdb.Metadata;
using Shoko.Plugin.Tmdb.Services;
using TMDbLib.Objects.Exceptions;
using Xunit;

using TmdbPlugin = Shoko.Plugin.Tmdb.Plugin;

namespace Shoko.Tests.Plugin.Tmdb;

/// <summary>
///   The provider as the core sees it: its pause, its failures, its episode
///   matching and that it stays out of the server until the cut-over.
/// </summary>
public sealed class TmdbMetadataProviderTests : IDisposable
{
    private readonly TmdbServiceHarness _harness = new();

    public void Dispose()
        => _harness.Dispose();

    #region Registration

    [Fact]
    public void TheProviderIsRegisteredAndDiscoverable()
    {
        var services = new ServiceCollection();

        TmdbPlugin.RegisterServices(services, Mock.Of<IApplicationPaths>());

        Assert.Contains(services, service => service.ServiceType == typeof(TmdbMetadataProvider));
        // The server only discovers public providers.
        Assert.True(typeof(TmdbMetadataProvider).IsPublic);
    }

    [Fact]
    public void TheSourceIconIsThePluginIcon()
        => Assert.Equal(new TmdbPlugin().EmbeddedIconResourceName, _harness.Provider.EmbeddedIconResourceName);

    [Theory]
    [InlineData(null, "https://image.tmdb.org/t/p/original/{0}")]
    [InlineData("https://cdn.example.com/tmdb", "https://cdn.example.com/tmdb/original/{0}")]
    [InlineData("https://cdn.example.com/w500/{0}", "https://cdn.example.com/w500/{0}")]
    [InlineData("not a url", "https://image.tmdb.org/t/p/original/{0}")]
    public void TheImageTemplateFollowsTheConfiguredServer(string? imageCdnUrl, string template)
        => Assert.Equal(template, TmdbBackgroundService.DefaultTemplate(imageCdnUrl));

    #endregion

    #region Configuration & Pausing

    [Fact]
    public void WithoutAnApiKeyTheProviderSaysWhatIsMissing()
    {
        _harness.Configuration.UserApiKey = null;

        Assert.False(_harness.Provider.IsConfigured);
        Assert.NotNull(_harness.Provider.NotConfiguredReason);
    }

    [Fact]
    public async Task ASearchTmdbCannotAnswerIsUnavailable()
    {
        _harness.Routes.Status("search/tv", HttpStatusCode.BadGateway).Status("configuration", HttpStatusCode.BadGateway);

        var exception = await Assert.ThrowsAsync<MetadataProviderUnavailableException>(() => _harness.Provider.SearchSeries(new() { Query = "Journey" }, TestContext.Current.CancellationToken));

        Assert.Equal(MetadataSource.TMDB, exception.MetadataSource);
    }

    [Theory]
    [InlineData(typeof(HttpRequestException), true)]
    [InlineData(typeof(InvalidOperationException), false)]
    public void OnlyAFailureThatPassesIsUnavailable(Type exceptionType, bool unavailable)
        => Assert.Equal(unavailable, TmdbMetadataProvider.ToUnavailable((Exception)Activator.CreateInstance(exceptionType)!, null) is not null);

    [Fact]
    public void AServerErrorWaitsOutWhatIsLeftOfThePause()
    {
        var unavailable = TmdbMetadataProvider.ToUnavailable(new GeneralHttpException(HttpStatusCode.InternalServerError), TimeSpan.FromMinutes(3));

        Assert.Equal(TimeSpan.FromMinutes(3), unavailable?.RetryAfter);
    }

    #endregion

    #region Episodes

    [Fact]
    public async Task EpisodesAreMatchedWithinASeasonAndItsSpecials()
    {
        _harness.RouteShow();
        await _harness.Refresh.RefreshShow(1001, new() { QuickRefresh = true }, TestContext.Current.CancellationToken);
        var handed = CaptureCandidates();

        await _harness.Provider.MatchEpisodes(Mock.Of<IAnidbAnime>(), [], TmdbIds.Series(1001), TmdbIds.Season(2001), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([TmdbIds.Episode(3000), TmdbIds.Episode(3001), TmdbIds.Episode(3002)], handed.Single().Select(episode => episode.ID));
    }

    [Fact]
    public async Task ASeasonOfAnotherShowMatchesNothing()
    {
        _harness.RouteShow();
        await _harness.Refresh.RefreshShow(1001, new() { QuickRefresh = true }, TestContext.Current.CancellationToken);
        var handed = CaptureCandidates();

        var matches = await _harness.Provider.MatchEpisodes(Mock.Of<IAnidbAnime>(), [], TmdbIds.Series(1001), TmdbIds.Season(9999), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(matches);
        Assert.Empty(handed);
    }

    [Fact]
    public async Task EpisodesOtherAnimeAreLinkedToAreLeftOut()
    {
        _harness.RouteShow();
        await _harness.Refresh.RefreshShow(1001, new() { QuickRefresh = true }, TestContext.Current.CancellationToken);
        _harness.CrossReferences.Setup(mock => mock.GetEpisodeLinksInto(TmdbIds.Series(1001))).Returns(
        [
            Mock.Of<IMetadataEpisodeCrossReference>(link => link.AnidbAnimeID == 7 && link.ProviderID == TmdbIds.Episode(3001)),
        ]);
        var handed = CaptureCandidates();

        await _harness.Provider.MatchEpisodes(
            Mock.Of<IAnidbAnime>(anime => anime.AnidbID == 42),
            [],
            TmdbIds.Series(1001),
            considerOtherLinks: true,
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.DoesNotContain(TmdbIds.Episode(3001), handed.Single().Select(episode => episode.ID));
    }

    private List<IReadOnlyList<IEpisode>> CaptureCandidates()
    {
        var handed = new List<IReadOnlyList<IEpisode>>();
        _harness.Engine
            .Setup(engine => engine.MatchEpisodes(It.IsAny<IReadOnlyList<IAnidbEpisode>>(), It.IsAny<IReadOnlyList<IEpisode>>(), It.IsAny<IReadOnlyList<IMetadataEpisodeCrossReference>?>(), It.IsAny<EpisodeMatchOptions?>()))
            .Callback((IReadOnlyList<IAnidbEpisode> _, IReadOnlyList<IEpisode> candidates, IReadOnlyList<IMetadataEpisodeCrossReference>? _, EpisodeMatchOptions? _) => handed.Add(candidates))
            .Returns([]);
        return handed;
    }

    #endregion
}
