using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Models;

/// <summary>
/// Covers <see cref="AnimeEpisode.DefaultTitle"/>, which every episode falls back to when the user
/// has set no override and no preferred title matches, and the user's title taking its place. The
/// titles come from the text store, so these run against a cache-only store and real repositories
/// seeded from memory rather than a database.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class AnimeEpisodeTitleTests
{
    private static TitleStub Title(string title, TitleLanguage language)
        => new()
        {
            Source = MetadataSource.AniDB,
            Language = language,
            LanguageCode = language is TitleLanguage.English ? "en" : "ja",
            Value = title,
            Type = AnidbText.EpisodeTitleType(language),
        };

    private static AniDB_Episode Episode(int episodeID, EpisodeType type = EpisodeType.Episode, int number = 1)
        => new() { AniDB_EpisodeID = episodeID, EpisodeID = episodeID, AnimeID = 1, EpisodeType = type, EpisodeNumber = number };

    /// <summary>
    /// Installs the AniDB episodes and a text store holding the given titles for episode 100.
    /// </summary>
    private static RepoFactoryScope ScopeWith(AniDB_Episode[] episodes, params ITitle[] titles)
        => ScopeWith(episodes, out _, titles);

    /// <summary>
    /// Installs the AniDB episodes and a text store holding the given titles for episode 100,
    /// handing back the text manager put in place.
    /// </summary>
    private static RepoFactoryScope ScopeWith(AniDB_Episode[] episodes, out MetadataTextManager manager, params ITitle[] titles)
    {
        var store = new MetadataTextStore(new TextCache(), new CacheOnlyRowWriter());
        // No episode is linked to any other source, so only AniDB's titles are chosen from.
        var service = new Mock<IMetadataService>();
        service.Setup(s => s.GetEpisodeCrossReferences(It.IsAny<int>(), It.IsAny<MetadataSource?>())).Returns([]);
        service.Setup(s => s.GetMovieCrossReferences(It.IsAny<int>(), It.IsAny<MetadataSource?>())).Returns([]);
        service.Setup(s => s.GetMovieCrossReferencesForSeries(It.IsAny<int>(), It.IsAny<MetadataSource?>())).Returns([]);
        manager = TestTextManager.Build(store, service.Object);
        var scope = new RepoFactoryScope()
            .Set(manager)
            .With<AniDB_EpisodeRepository, int, AniDB_Episode>(e => e.AniDB_EpisodeID, episodes);
        store.SetTitles(new(MetadataSource.AniDB, MetadataEntityType.Episode, "100"), MetadataSource.AniDB, titles);
        return scope;
    }

    [Fact]
    public void DefaultTitle_UsesTheEnglishTitleWhenOneExists()
    {
        using var scope = ScopeWith([Episode(100)], Title("The English One", TitleLanguage.English));

        var episode = new AnimeEpisode { AniDB_EpisodeID = 100 };

        Assert.Equal("The English One", episode.DefaultTitle.Value);
        Assert.Equal(TitleLanguage.English, episode.DefaultTitle.Language);
    }

    [Fact]
    public void DefaultTitle_IgnoresTitlesInOtherLanguages()
    {
        using var scope = ScopeWith(
            [Episode(100)],
            Title("Nihongo", TitleLanguage.Japanese),
            Title("The English One", TitleLanguage.English));

        var episode = new AnimeEpisode { AniDB_EpisodeID = 100 };

        Assert.Equal("The English One", episode.DefaultTitle.Value);
    }

    [Fact]
    public void DefaultTitle_IsAniDBsGenericTitleWhenTheEpisodeHasNoEnglishTitle()
    {
        using var scope = ScopeWith([Episode(100, EpisodeType.Special, 3)], Title("Nihongo", TitleLanguage.Japanese));

        var episode = new AnimeEpisode { AniDB_EpisodeID = 100 };

        Assert.Equal("Episode S3", episode.DefaultTitle.Value);
        Assert.Equal(TitleLanguage.English, episode.DefaultTitle.Language);
        Assert.True(episode.DefaultTitle.IsSynthesized);
    }

    [Fact]
    public void DefaultTitle_IgnoresTitlesBelongingToOtherEpisodes()
    {
        using var scope = ScopeWith([Episode(100), Episode(999)], Title("The English One", TitleLanguage.English));

        var episode = new AnimeEpisode { AniDB_EpisodeID = 999 };

        Assert.Equal("Episode 1", episode.DefaultTitle.Value);
    }

    [Fact]
    public void DefaultTitle_FallsBackToAPlaceholderNamingTheEpisode()
    {
        using var scope = ScopeWith([]);

        var episode = new AnimeEpisode { AniDB_EpisodeID = 100 };

        Assert.Equal("<AniDB Episode 100>", episode.DefaultTitle.Value);
        Assert.Equal(TitleLanguage.Unknown, episode.DefaultTitle.Language);
        Assert.Equal(MetadataSource.Shoko, episode.DefaultTitle.Source);
    }

    [Fact]
    public void Title_PrefersTheUsersTitleOverAnyStoredTitle()
    {
        using var scope = ScopeWith([Episode(100)], out var manager, Title("The English One", TitleLanguage.English));
        var episode = new AnimeEpisode { AnimeEpisodeID = 7, AniDB_EpisodeID = 100 };

        Assert.True(manager.SetCustomTitle(((IMetadata)episode).ID, "What The User Called It"));

        Assert.Equal("What The User Called It", episode.Title);
        Assert.Equal("What The User Called It", episode.CustomTitle?.Value);
        Assert.Equal(MetadataSource.User, episode.CustomTitle?.Source);
    }

    [Fact]
    public void Title_FallsBackOnceTheUsersTitleIsRemoved()
    {
        using var scope = ScopeWith([Episode(100)], out var manager, Title("The English One", TitleLanguage.English));
        var episode = new AnimeEpisode { AnimeEpisodeID = 7, AniDB_EpisodeID = 100 };
        var entry = ((IMetadata)episode).ID;

        Assert.True(manager.SetCustomTitle(entry, "What The User Called It"));
        Assert.Equal("What The User Called It", episode.Title);

        // A blank title removes the user's.
        Assert.True(manager.SetCustomTitle(entry, "  "));

        Assert.Null(episode.CustomTitle);
        Assert.Equal("The English One", episode.Title);
    }
}
