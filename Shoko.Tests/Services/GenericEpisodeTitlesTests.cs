using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Services;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers <see cref="GenericEpisodeTitles"/>: which titles are an episode's
/// generic title, which only look like one, and the titles it makes up.
/// </summary>
public class GenericEpisodeTitlesTests
{
    [Theory]
    [InlineData("Episode 5", EpisodeType.Episode, 5, true)]
    [InlineData(" episode 5 ", EpisodeType.Episode, 5, true)]
    [InlineData("Episode S1", EpisodeType.Special, 1, true)]
    [InlineData("Episode C2", EpisodeType.Credits, 2, true)]
    [InlineData("Special 1", EpisodeType.Special, 1, false)]
    [InlineData("Opening 2", EpisodeType.Credits, 2, false)]
    [InlineData("Folge 5", EpisodeType.Episode, 5, true)]
    [InlineData("Épisode 3", EpisodeType.Episode, 3, true)]
    [InlineData("第5話", EpisodeType.Episode, 5, true)]
    [InlineData("第 15 集", EpisodeType.Episode, 15, true)]
    [InlineData("5화", EpisodeType.Episode, 5, true)]
    [InlineData("12. díl", EpisodeType.Episode, 12, true)]
    [InlineData("Episode 17", EpisodeType.Episode, 1, false)]
    [InlineData("Episode 1", EpisodeType.Special, 1, false)]
    [InlineData("Special 1", EpisodeType.Episode, 1, false)]
    [InlineData("Episode S1", EpisodeType.Episode, 1, false)]
    [InlineData("Episode 5: The Return", EpisodeType.Episode, 5, false)]
    [InlineData("Invasion of the Scaled Dragons", EpisodeType.Episode, 1, false)]
    [InlineData("", EpisodeType.Episode, 1, false)]
    public void AnEpisodesGenericTitleNamesItsOwnTypeAndNumber(string title, EpisodeType type, int number, bool expected)
        => Assert.Equal(expected, GenericEpisodeTitles.IsGeneric(title, type, number));

    [Theory]
    [InlineData("Episode 17", true)]
    [InlineData("Episode C2", true)]
    [InlineData("Opening 2", false)]
    [InlineData("Ending 3", false)]
    [InlineData("Special 1", false)]
    [InlineData("Episodio 4", true)]
    [InlineData("Episode Seventeen", false)]
    [InlineData("Part 1 of 2", false)]
    [InlineData(null, false)]
    public void ATitleLooksGenericWhateverItsNumber(string? title, bool expected)
        => Assert.Equal(expected, GenericEpisodeTitles.LooksGeneric(title));

    [Fact]
    public void OnlyAniDBsExactEnglishFormIsItsGenericTitle()
    {
        Assert.True(GenericEpisodeTitles.IsEnglishGeneric(" episode s1 ", EpisodeType.Special, 1));
        Assert.False(GenericEpisodeTitles.IsEnglishGeneric("Folge 5", EpisodeType.Episode, 5));
        Assert.False(GenericEpisodeTitles.IsEnglishGeneric("Episode 5", EpisodeType.Episode, 6));
        Assert.False(GenericEpisodeTitles.IsEnglishGeneric(null, EpisodeType.Episode, 1));
    }

    [Fact]
    public void AMadeUpTitleIsInTheFirstLanguageWithAFormElseEnglish()
    {
        var japanese = GenericEpisodeTitles.Synthesize(EpisodeType.Episode, 7, [TitleLanguage.Swedish, TitleLanguage.Japanese]);
        var english = GenericEpisodeTitles.Synthesize(EpisodeType.Episode, 7, [TitleLanguage.Swedish]);
        var special = GenericEpisodeTitles.Synthesize(EpisodeType.Special, 2, [TitleLanguage.Japanese]);

        Assert.Equal(("第7話", TitleLanguage.Japanese, "ja"), (japanese.Value, japanese.Language, japanese.LanguageCode));
        Assert.Equal(("Episode 7", TitleLanguage.English), (english.Value, english.Language));
        Assert.Equal(("Special 2", TitleLanguage.English), (special.Value, special.Language));
        Assert.True(GenericEpisodeTitles.IsGeneric(japanese.Value, EpisodeType.Episode, 7));
    }
}
