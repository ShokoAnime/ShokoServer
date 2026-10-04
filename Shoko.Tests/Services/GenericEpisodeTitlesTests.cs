using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Services;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers <see cref="GenericEpisodeTitles"/>: which titles are an episode's
/// generic title, which only look like one, and the titles it synthesizes.
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
    [InlineData("Episode 05", EpisodeType.Episode, 5, true)]
    [InlineData("第１２話", EpisodeType.Episode, 12, true)]
    [InlineData("第二十五話", EpisodeType.Episode, 25, true)]
    [InlineData("第一百零二集", EpisodeType.Episode, 102, true)]
    [InlineData("第十话", EpisodeType.Episode, 10, true)]
    [InlineData("第十一回", EpisodeType.Episode, 11, true)]
    [InlineData("第十一話", EpisodeType.Episode, 1, false)]
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
    [InlineData("Episode 104", true)]
    [InlineData("第 15 集", true)]
    [InlineData("에피소드1", true)]
    [InlineData("에피소드 1", true)]
    [InlineData("3화", true)]
    [InlineData("エピソード1", true)]
    [InlineData("エピソード 1", true)]
    [InlineData("第二集", true)]
    [InlineData("第一話", true)]
    [InlineData("第一话", true)]
    [InlineData("第六回", true)]
    [InlineData("Episode Seventeen", false)]
    [InlineData("Part 1 of 2", false)]
    [InlineData("Part 2", false)]
    [InlineData("Mini #12", false)]
    [InlineData("Special #01", false)]
    [InlineData("Volume 1", false)]
    [InlineData("Lektion 01", false)]
    [InlineData("Act 2", false)]
    [InlineData("Picture Drama 7", false)]
    [InlineData("Tachikomatic Days EP04", false)]
    [InlineData("古古怪界大作战#1", false)]
    [InlineData("#1", false)]
    [InlineData("Epidosio 5", false)]
    [InlineData("第1章", false)]
    [InlineData("第1巻", false)]
    [InlineData("第一夜", false)]
    [InlineData("第一章", false)]
    [InlineData("第二幕", false)]
    [InlineData("总第12集", true)]
    [InlineData("總第十二集", true)]
    [InlineData("Bölüm 4", true)]
    [InlineData("5 эпизод", true)]
    [InlineData("Episodes 1", false)]
    [InlineData("Ending 2a", false)]
    [InlineData("Episode 3 Ending", false)]
    [InlineData("Web Preview Episode 4", false)]
    [InlineData("PV 2", false)]
    [InlineData("CM 3", false)]
    [InlineData("Promo 2", false)]
    [InlineData("Mini Anime 5", false)]
    [InlineData("PV第2弾", false)]
    [InlineData("第2弾PV", false)]
    [InlineData("第5話予告映像", false)]
    [InlineData("PV Dai 2 Dan", false)]
    [InlineData("Dai 5 Wa Yokoku", false)]
    [InlineData("일화", false)]
    [InlineData("이화", false)]
    [InlineData("TBA", true)]
    [InlineData(" tbd ", true)]
    [InlineData("TBA.", true)]
    [InlineData("TBA 2", false)]
    [InlineData("To Be Announced", false)]
    [InlineData("Untitled", false)]
    [InlineData("Unknown", false)]
    [InlineData("?", false)]
    [InlineData("???", false)]
    [InlineData("未定", false)]
    [InlineData("内容未定", false)]
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
    public void ASynthesizedTitleIsInTheFirstLanguageWithAFormElseEnglish()
    {
        var japanese = GenericEpisodeTitles.Synthesize(EpisodeType.Episode, 7, false, [TitleLanguage.Swedish, TitleLanguage.Japanese]);
        var english = GenericEpisodeTitles.Synthesize(EpisodeType.Episode, 7, false, [TitleLanguage.Swedish]);

        Assert.Equal(("第7話", TitleLanguage.Japanese, "ja"), (japanese.Value, japanese.Language, japanese.LanguageCode));
        Assert.Equal(("Episode 7", TitleLanguage.English), (english.Value, english.Language));
        Assert.True(GenericEpisodeTitles.IsGeneric(japanese.Value, EpisodeType.Episode, 7));
    }

    [Theory]
    [InlineData(EpisodeType.Special, 3, true, "Episode S3")]
    [InlineData(EpisodeType.Credits, 1, true, "Episode C1")]
    [InlineData(EpisodeType.Other, 2, true, "Episode O2")]
    [InlineData(EpisodeType.Episode, 4, true, "Episode 4|第4話")]
    [InlineData(EpisodeType.Special, 3, false, "Episode 3|第3話")]
    [InlineData(EpisodeType.Credits, 1, false, "Episode 1|第1話")]
    public void AniDBsOtherTypesTakeItsTypeLetterAndEveryOtherSourceThePlainForm(EpisodeType type, int number, bool anidbForms, string expected)
    {
        var titles = GenericEpisodeTitles.SynthesizeAll(type, number, anidbForms, [TitleLanguage.English, TitleLanguage.Japanese]);

        Assert.Equal(expected.Split('|'), titles.Select(title => title.Value));
        Assert.All(titles, title => Assert.True(GenericEpisodeTitles.LooksGeneric(title.Value), title.Value));
    }

    [Fact]
    public void TitlesAreSynthesizedInEveryLanguageWithAFormInOrderThenEnglishTheFirstAsMain()
    {
        var episode = GenericEpisodeTitles.SynthesizeAll(EpisodeType.Episode, 7, false, [TitleLanguage.German, TitleLanguage.Swedish, TitleLanguage.Japanese]);
        var season = GenericEpisodeTitles.SynthesizeSeasonAll(2, [TitleLanguage.French, TitleLanguage.English, TitleLanguage.Japanese]);
        var specials = GenericEpisodeTitles.SynthesizeSeasonAll(0, [TitleLanguage.German]);

        Assert.Equal(["Folge 7", "第7話", "Episode 7"], episode.Select(title => title.Value));
        Assert.Equal([TitleType.Main, TitleType.None, TitleType.None], episode.Select(title => title.Type));
        Assert.All(episode, title => Assert.True(title.IsSynthesized));
        Assert.Equal(["Saison 2", "Season 2", "シーズン2"], season.Select(title => title.Value));
        Assert.Equal(["Specials"], specials.Select(title => title.Value));
    }

    [Theory]
    [InlineData(TitleLanguage.Japanese, "第7話")]
    [InlineData(TitleLanguage.ChineseTraditional, "第7集")]
    [InlineData(TitleLanguage.Korean, "7화")]
    [InlineData(TitleLanguage.French, "Épisode 7")]
    [InlineData(TitleLanguage.Turkish, "7. Bölüm")]
    public void ASynthesizedTitleTakesTheLanguagesFirstFormInArabicDigits(TitleLanguage language, string expected)
        => Assert.Equal(expected, GenericEpisodeTitles.Synthesize(EpisodeType.Episode, 7, false, [language]).Value);

    [Fact]
    public void EveryLanguageHasEpisodeSeasonAndSpecialsFormsThatAreAllRecognised()
        => Assert.All(GenericEpisodeTitles.Forms, entry =>
        {
            Assert.NotEmpty(entry.Value.Episodes);
            Assert.Contains("{0}", entry.Value.Season);
            Assert.NotEmpty(entry.Value.Specials);
            Assert.All(entry.Value.Episodes, form => Assert.True(GenericEpisodeTitles.IsGeneric(form.Replace("{0}", "7"), EpisodeType.Episode, 7), form));
            Assert.True(GenericEpisodeTitles.LooksLikeSeasonName(entry.Value.Season.Replace("{0}", "3")), entry.Value.Season);
            Assert.All(entry.Value.Specials, form => Assert.True(GenericEpisodeTitles.LooksLikeSeasonName(form), form));
        });

    [Theory]
    [InlineData("Season 12", true)]
    [InlineData(" staffel 3 ", true)]
    [InlineData("シーズン2", true)]
    [InlineData("3. Sezon", true)]
    [InlineData("Specials", true)]
    [InlineData("Épisodes spéciaux", true)]
    [InlineData("第二季", true)]
    [InlineData("第 2 季", true)]
    [InlineData("シーズン２", true)]
    [InlineData("Season 01", true)]
    [InlineData("第二幕", false)]
    [InlineData("Season of Love", false)]
    [InlineData("The Final Season", false)]
    public void ASeasonNameLooksGenericInAnyKnownLanguageWhateverItsNumber(string title, bool expected)
        => Assert.Equal(expected, GenericEpisodeTitles.LooksLikeSeasonName(title));

    [Theory]
    [InlineData("Season 8", 8, true)]
    [InlineData("Season 11", 8, false)]
    [InlineData("Staffel 2", 2, true)]
    [InlineData("第2季", 2, true)]
    [InlineData("第二季", 2, true)]
    [InlineData("Staffel 2", 3, false)]
    [InlineData("Specials", 0, true)]
    [InlineData("Épisodes spéciaux", 0, true)]
    [InlineData("Specials", 1, false)]
    [InlineData("The Final Season", 2, false)]
    public void ASeasonsGenericNameCarriesItsOwnNumber(string title, int number, bool expected)
        => Assert.Equal(expected, GenericEpisodeTitles.IsGenericFor(MetadataEntityType.Season, title, new(number)));

    [Theory]
    [InlineData("Episode 1", EpisodeType.Episode, 1, false, true)]
    [InlineData("Episode 13", EpisodeType.Episode, 1, false, false)]
    [InlineData("第5話", EpisodeType.Episode, 5, true, true)]
    [InlineData("Folge 5", EpisodeType.Episode, 6, false, false)]
    [InlineData("Episode 3", EpisodeType.Special, 3, false, true)]
    [InlineData("Episode 3", EpisodeType.Special, 3, true, false)]
    [InlineData("Episode S3", EpisodeType.Special, 3, true, true)]
    [InlineData("Episode C1", EpisodeType.Credits, 1, true, true)]
    [InlineData("Episode S2", EpisodeType.Special, 3, true, false)]
    [InlineData("Episode S3", EpisodeType.Special, 3, false, false)]
    [InlineData("Episode S3", EpisodeType.Episode, 3, true, false)]
    [InlineData("TBA", EpisodeType.Episode, 5, false, true)]
    public void AnEpisodesGenericTitleIsTheOneSynthesizedForItsOwnNumber(string title, EpisodeType type, int number, bool anidbForms, bool expected)
        => Assert.Equal(expected, GenericEpisodeTitles.IsGenericFor(MetadataEntityType.Episode, title, new(number, type, anidbForms)));

    [Fact]
    public void WithoutTheEntrysNumberOnlyAnEpisodesStandInIsGeneric()
    {
        Assert.True(GenericEpisodeTitles.IsGenericFor(MetadataEntityType.Episode, "TBD", null));
        Assert.False(GenericEpisodeTitles.IsGenericFor(MetadataEntityType.Episode, "Episode 1", null));
        Assert.False(GenericEpisodeTitles.IsGenericFor(MetadataEntityType.Season, "Season 1", null));
        Assert.False(GenericEpisodeTitles.IsGenericFor(MetadataEntityType.Series, "Episode 1", new(1)));
    }
}
