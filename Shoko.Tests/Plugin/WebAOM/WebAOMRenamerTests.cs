using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Enums;
using Shoko.Abstractions.Video.Hashing;
using Shoko.Abstractions.Video.Media;
using Shoko.Abstractions.Video.Release;
using Shoko.Abstractions.Video.Relocation;
using Shoko.Abstractions.Video.Services;
using Shoko.Plugin.WebAOM;
using Xunit;

namespace Shoko.Tests.Plugin.WebAOM;

/// <summary>
///   Runs the bundled plugin's WebAOM renamer over a file built from the
///   abstractions alone, as the server hands it one.
/// </summary>
public class WebAOMRenamerTests
{
    #region Fixture

    private static readonly WebAOMRenamer _renamer = new(NullLogger<WebAOMRenamer>.Instance, Mock.Of<IVideoRelocationService>());

    private static ITitle Title(string value, TitleLanguage language, TitleType type, MetadataSource? source = null)
    {
        var title = new Mock<ITitle>();
        title.SetupGet(t => t.Value).Returns(value);
        title.SetupGet(t => t.Language).Returns(language);
        title.SetupGet(t => t.Type).Returns(type);
        title.SetupGet(t => t.Source).Returns(source ?? MetadataSource.AniDB);
        return title.Object;
    }

    private static Mock<IAnidbAnime> Anime(params ITitle[] titles)
    {
        var anime = new Mock<IAnidbAnime>();
        anime.SetupGet(a => a.AnidbID).Returns(7307);
        anime.SetupGet(a => a.Titles).Returns(titles);
        anime.SetupGet(a => a.DefaultTitle).Returns(titles[0]);
        anime.SetupGet(a => a.Type).Returns(AnimeType.TV);
        anime.SetupGet(a => a.AirDate).Returns(new PartialDateOnly(2010, 7, 5));
        anime.SetupGet(a => a.EpisodeCounts).Returns(new EpisodeCounts { Episodes = 12, Specials = 1 });
        return anime;
    }

    private static IShokoEpisode Episode(IAnidbAnime anime, string englishTitle)
    {
        var anidbEpisode = new Mock<IAnidbEpisode>();
        anidbEpisode.SetupGet(e => e.AnidbID).Returns(113331);
        anidbEpisode.SetupGet(e => e.AnidbAnimeID).Returns(anime.AnidbID);
        anidbEpisode.SetupGet(e => e.Type).Returns(EpisodeType.Episode);
        anidbEpisode.SetupGet(e => e.EpisodeNumber).Returns(1);
        anidbEpisode.SetupGet(e => e.Series).Returns(anime);
        anidbEpisode.SetupGet(e => e.DefaultTitle).Returns(Title(englishTitle, TitleLanguage.English, TitleType.Main));
        anidbEpisode.SetupGet(e => e.Titles).Returns([Title(englishTitle, TitleLanguage.English, TitleType.Main)]);
        var episode = new Mock<IShokoEpisode>();
        episode.SetupGet(e => e.AnidbEpisode).Returns(anidbEpisode.Object);
        return episode.Object;
    }

    private static IVideo Video()
    {
        var group = new Mock<IReleaseGroup>();
        group.SetupGet(g => g.ID).Returns("1234");
        group.SetupGet(g => g.Name).Returns("Coalgirls Fansubs");
        group.SetupGet(g => g.ShortName).Returns("Coalgirls");
        var release = new Mock<IReleaseInfo>();
        release.SetupGet(r => r.Group).Returns(group.Object);
        release.SetupGet(r => r.Version).Returns(2);
        release.SetupGet(r => r.Source).Returns(ReleaseSource.BluRay);
        release.SetupGet(r => r.ReleaseURI).Returns("https://anidb.net/file/123456");
        var crc = new Mock<IHashDigest>();
        crc.SetupGet(h => h.Type).Returns("CRC32");
        crc.SetupGet(h => h.Value).Returns("90cc6dc1");
        var stream = new Mock<IVideoStream>();
        stream.SetupGet(s => s.Width).Returns(1920);
        stream.SetupGet(s => s.Height).Returns(1080);
        var mediaInfo = new Mock<IMediaInfo>();
        mediaInfo.SetupGet(m => m.VideoStream).Returns(stream.Object);
        var video = new Mock<IVideo>();
        video.SetupGet(v => v.ED2K).Returns("0123456789abcdef0123456789abcdef");
        video.SetupGet(v => v.ReleaseInfo).Returns(release.Object);
        video.SetupGet(v => v.Hashes).Returns([crc.Object]);
        video.SetupGet(v => v.MediaInfo).Returns(mediaInfo.Object);
        return video.Object;
    }

    private static IVideoFile VideoFile()
    {
        var file = new Mock<IVideoFile>();
        file.SetupGet(f => f.RelativePath).Returns(Path.Join("Drop", "[Coalgirls] HOTD - 01.mkv"));
        file.SetupGet(f => f.Path).Returns(Path.Join(Path.GetTempPath(), "Drop", "[Coalgirls] HOTD - 01.mkv"));
        return file.Object;
    }

    private static RelocationContext<WebAOMSettings> Context(string script, IAnidbAnime anime, int maxEpisodeLength = 33, string englishEpisodeTitle = "Spring of the Dead")
        => new(
            new RelocationContext
            {
                MoveEnabled = false,
                RenameEnabled = true,
                File = VideoFile(),
                Video = Video(),
                Episodes = [Episode(anime, englishEpisodeTitle)],
            },
            new WebAOMSettings { Script = script, MaxEpisodeLength = maxEpisodeLength }
        );

    #endregion

    #region Renaming

    [Fact]
    public void Tags_AreFilledFromTheAbstractions()
    {
        var anime = Anime(
            Title("Highschool of the Dead", TitleLanguage.Romaji, TitleType.Main),
            Title("High School of the Dead", TitleLanguage.English, TitleType.Official)
        );
        var script = """
            DO ADD '[%grp] %ann - %enr'
            IF I(eng) DO ADD ' (%eng)'
            IF F(!1) DO ADD 'v%ver'
            DO ADD ' [%aid-%eid-%fid] %typ %yea %src (%res) [%CRC]'
            """;

        var result = _renamer.GetPath(Context(script, anime.Object));

        Assert.Null(result.Error);
        Assert.Equal("[Coalgirls] Highschool of the Dead - 01 (High School of the Dead)v2 [7307-113331-123456] tv series 2010 bluray (1920x1080) [90CC6DC1].mkv", result.FileName);
    }

    [Fact]
    public void Tests_OnlyLookAtTheTitlesAniDBGave()
    {
        // An English title another source added to the anime is not one AniDB gave it.
        var anime = Anime(
            Title("Highschool of the Dead", TitleLanguage.Romaji, TitleType.Main),
            Title("High School of the Dead", TitleLanguage.English, TitleType.Official, MetadataSource.User)
        );
        var script = """
            IF I(eng) DO ADD '%eng'
            IF I(!eng) DO ADD '%ann'
            """;

        var result = _renamer.GetPath(Context(script, anime.Object));

        Assert.Equal("Highschool of the Dead.mkv", result.FileName);
    }

    [Fact]
    public void EpisodeNames_AreCutToTheMaximumLength()
    {
        var anime = Anime(Title("Highschool of the Dead", TitleLanguage.Romaji, TitleType.Main));

        var result = _renamer.GetPath(Context("DO ADD '%epn'", anime.Object, maxEpisodeLength: 10));

        Assert.Equal("Spring of….mkv", result.FileName);
    }

    [Fact]
    public void Fail_LeavesTheFileAsItIs()
    {
        var anime = Anime(Title("Highschool of the Dead", TitleLanguage.Romaji, TitleType.Main));

        var script = """
            DO ADD '%ann'
            IF A(7307) DO FAIL
            """;

        var result = _renamer.GetPath(Context(script, anime.Object));

        Assert.Null(result.FileName);
        Assert.Null(result.ManagedFolder);
    }

    [Fact]
    public void AnEmptyScript_IsAnError()
    {
        var anime = Anime(Title("Highschool of the Dead", TitleLanguage.Romaji, TitleType.Main));

        var result = _renamer.GetPath(Context(string.Empty, anime.Object));

        Assert.NotNull(result.Error);
    }

    [Fact]
    public void AnUntypedCall_IsAnsweredByTheConfiguredPath_OnlyWithTheConfiguration()
    {
        var anime = Anime(Title("Highschool of the Dead", TitleLanguage.Romaji, TitleType.Main));
        IRelocationProvider provider = _renamer;

        var configured = provider.GetPath(Context("DO ADD '%ann'", anime.Object));
        var unconfigured = provider.GetPath(new RelocationContext { File = VideoFile(), Video = Video() });

        Assert.Equal("Highschool of the Dead.mkv", configured.FileName);
        Assert.NotNull(unconfigured.Error);
    }

    #endregion

    #region Moving

    [Fact]
    public void GroupAwareSorting_PlacesTheFileUnderItsGroup_InTheFirstDestination()
    {
        var series = new Mock<IShokoSeries>();
        series.SetupGet(s => s.Title).Returns("Highschool of the Dead");
        series.SetupGet(s => s.Restricted).Returns(false);
        var group = new Mock<IShokoGroup>();
        group.SetupGet(g => g.Title).Returns("Dead Things");
        group.SetupGet(g => g.Series).Returns([series.Object, Mock.Of<IShokoSeries>()]);
        var source = new Mock<IManagedFolder>();
        source.SetupGet(f => f.Path).Returns(Path.Join(Path.GetTempPath(), "Drop"));
        source.SetupGet(f => f.DropFolderType).Returns(DropFolderType.Source);
        var destination = new Mock<IManagedFolder>();
        destination.SetupGet(f => f.Path).Returns(Path.Join(Path.GetTempPath(), "Anime"));
        destination.SetupGet(f => f.DropFolderType).Returns(DropFolderType.Destination);
        var anime = Anime(Title("Highschool of the Dead", TitleLanguage.Romaji, TitleType.Main));
        var context = new RelocationContext<WebAOMSettings>(
            new RelocationContext
            {
                MoveEnabled = true,
                RenameEnabled = false,
                File = VideoFile(),
                Video = Video(),
                Episodes = [Episode(anime.Object, "Spring of the Dead")],
                Series = [series.Object],
                Groups = [group.Object],
                AvailableFolders = [source.Object, destination.Object],
            },
            new WebAOMSettings { GroupAwareSorting = true }
        );

        var result = _renamer.GetPath(context);

        Assert.Same(destination.Object, result.ManagedFolder);
        Assert.Equal(Path.Join("Dead Things", "Highschool of the Dead"), result.Path);
    }

    #endregion
}
