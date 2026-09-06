using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Models;

/// <summary>
/// Covers <see cref="AnimeEpisode.DefaultTitle"/>, which every episode falls back to when the user
/// has set no override and no preferred title matches. It reads through
/// <c>RepoFactory.AniDB_Episode_Title</c>, so these run against real repositories seeded from
/// memory rather than a database.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class AnimeEpisodeTitleTests
{
    private static AniDB_Episode_Title Title(int id, int episodeID, string title, TitleLanguage language)
        => new() { AniDB_Episode_TitleID = id, AniDB_EpisodeID = episodeID, Title = title, Language = language };

    private static RepoFactoryScope ScopeWith(params AniDB_Episode_Title[] titles)
        => new RepoFactoryScope()
            .With<AniDB_Episode_TitleRepository, int, AniDB_Episode_Title>(t => t.AniDB_Episode_TitleID, titles);

    [Fact]
    public void DefaultTitle_UsesTheEnglishTitleWhenOneExists()
    {
        using var scope = ScopeWith(Title(1, 100, "The English One", TitleLanguage.English));

        var episode = new AnimeEpisode { AniDB_EpisodeID = 100 };

        Assert.Equal("The English One", episode.DefaultTitle.Value);
        Assert.Equal(TitleLanguage.English, episode.DefaultTitle.Language);
    }

    [Fact]
    public void DefaultTitle_IgnoresTitlesInOtherLanguages()
    {
        using var scope = ScopeWith(
            Title(1, 100, "Nihongo", TitleLanguage.Japanese),
            Title(2, 100, "The English One", TitleLanguage.English));

        var episode = new AnimeEpisode { AniDB_EpisodeID = 100 };

        Assert.Equal("The English One", episode.DefaultTitle.Value);
    }

    [Fact]
    public void DefaultTitle_IgnoresTitlesBelongingToOtherEpisodes()
    {
        using var scope = ScopeWith(Title(1, 999, "Someone Else's Title", TitleLanguage.English));

        var episode = new AnimeEpisode { AniDB_EpisodeID = 100 };

        Assert.Equal("<AniDB Episode 100>", episode.DefaultTitle.Value);
    }

    [Fact]
    public void DefaultTitle_FallsBackToAPlaceholderNamingTheEpisode()
    {
        using var scope = ScopeWith();

        var episode = new AnimeEpisode { AniDB_EpisodeID = 100 };

        Assert.Equal("<AniDB Episode 100>", episode.DefaultTitle.Value);
        Assert.Equal(TitleLanguage.Unknown, episode.DefaultTitle.Language);
        Assert.Equal(DataSource.None, episode.DefaultTitle.Source);
    }

    [Fact]
    public void DefaultTitle_IsResolvedOnceAndReused()
    {
        using var scope = ScopeWith(Title(1, 100, "The English One", TitleLanguage.English));

        var episode = new AnimeEpisode { AniDB_EpisodeID = 100 };

        Assert.Same(episode.DefaultTitle, episode.DefaultTitle);
    }

    [Fact]
    public void Title_PrefersTheUserOverrideOverAnyStoredTitle()
    {
        using var scope = ScopeWith(Title(1, 100, "The English One", TitleLanguage.English));

        var episode = new AnimeEpisode { AniDB_EpisodeID = 100, EpisodeNameOverride = "What The User Called It" };

        Assert.Equal("What The User Called It", episode.Title);
    }
}
