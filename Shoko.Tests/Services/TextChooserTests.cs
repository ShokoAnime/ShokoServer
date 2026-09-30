using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers <see cref="TextChooser"/>, the one way titles and overviews are
/// chosen: a user's overall and per-language picks, the language and source
/// walk, real titles before generic ones, the default and the made-up title.
/// </summary>
public class TextChooserTests
{
    #region Helpers

    private static readonly MetadataSource _anidb = MetadataSource.AniDB;

    private static readonly MetadataSource _tmdb = MetadataSource.TMDB;

    private sealed class Candidate : TitleStub, ITitle
    {
        public TextPreference Preference { get; init; }

        public bool IsEnabled { get; init; } = true;
    }

    private static Candidate Title(string value, MetadataSource source, TitleLanguage language, TitleType type = TitleType.Official,
        TextPreference preference = TextPreference.None, bool enabled = true)
        => new()
        {
            Source = source,
            Value = value,
            Language = language,
            LanguageCode = language switch { TitleLanguage.Japanese => "ja", TitleLanguage.German => "de", TitleLanguage.Romaji => "x-jat", TitleLanguage.Main => "x-main", _ => "en" },
            Type = type,
            Preference = preference,
            IsEnabled = enabled,
        };

    private static TitleChoice Choice(IReadOnlyList<TitleLanguage> languages, bool episode = false, ITitle? fallback = null, bool synthesize = false, bool synonyms = false)
        => new(
            languages,
            [_tmdb, _anidb],
            synonyms,
            episode,
            fallback,
            synthesize ? () => GenericEpisodeTitles.Synthesize(EpisodeType.Episode, 5, languages) : null
        );

    #endregion

    #region Picks

    [Fact]
    public void AUsersOverallPickWinsOutright()
    {
        IReadOnlyList<ITitle> titles =
        [
            Title("English", _tmdb, TitleLanguage.English),
            Title("Picked", MetadataSource.User, TitleLanguage.German, preference: TextPreference.Overall),
        ];

        Assert.Equal("Picked", TextChooser.ChooseTitle(titles, Choice([TitleLanguage.English]))?.Value);
    }

    [Fact]
    public void APickForALanguageWinsOnlyOnceTheOrderReachesItsLanguage()
    {
        IReadOnlyList<ITitle> titles =
        [
            Title("English", _tmdb, TitleLanguage.English),
            Title("Official", _tmdb, TitleLanguage.Japanese),
            Title("Picked", _anidb, TitleLanguage.Japanese, TitleType.Synonym, TextPreference.Language),
        ];

        Assert.Equal("English", TextChooser.ChooseTitle(titles, Choice([TitleLanguage.English, TitleLanguage.Japanese]))?.Value);
        Assert.Equal("Picked", TextChooser.ChooseTitle(titles, Choice([TitleLanguage.Japanese, TitleLanguage.English]))?.Value);
    }

    [Fact]
    public void ADisabledTextIsNeverChosenNotEvenWhenPicked()
    {
        IReadOnlyList<ITitle> titles =
        [
            Title("Disabled", _tmdb, TitleLanguage.English, preference: TextPreference.Overall, enabled: false),
            Title("Kept", _anidb, TitleLanguage.English),
        ];

        Assert.Equal("Kept", TextChooser.ChooseTitle(titles, Choice([TitleLanguage.English]))?.Value);
    }

    #endregion

    #region Walk

    [Fact]
    public void TheWalkIsByLanguageFirstThenBySource()
    {
        IReadOnlyList<ITitle> titles =
        [
            Title("Japanese", _tmdb, TitleLanguage.Japanese),
            Title("English", _anidb, TitleLanguage.English),
        ];

        Assert.Equal("English", TextChooser.ChooseTitle(titles, Choice([TitleLanguage.English, TitleLanguage.Japanese]))?.Value);
    }

    [Fact]
    public void XMainReadsTheSourcesMainTitleSuchAsItsInlineDefault()
    {
        IReadOnlyList<ITitle> titles =
        [
            Title("Main", _anidb, TitleLanguage.Romaji, TitleType.Main),
            Title("English", _tmdb, TitleLanguage.English),
        ];

        Assert.Equal("Main", TextChooser.ChooseTitle(titles, Choice([TitleLanguage.Main]))?.Value);
    }

    [Fact]
    public void ASynonymAnswersOnlyWhenSynonymsAreAllowed()
    {
        IReadOnlyList<ITitle> titles = [Title("Synonym", _tmdb, TitleLanguage.English, TitleType.Synonym)];

        Assert.Null(TextChooser.ChooseTitle(titles, Choice([TitleLanguage.English])));
        Assert.Equal("Synonym", TextChooser.ChooseTitle(titles, Choice([TitleLanguage.English], synonyms: true))?.Value);
    }

    [Fact]
    public void ASourceOutsideTheOrderIsNotWalked()
    {
        IReadOnlyList<ITitle> titles = [Title("Plugin", MetadataSource.User, TitleLanguage.English)];

        Assert.Null(TextChooser.ChooseTitle(titles, Choice([TitleLanguage.English])));
    }

    #endregion

    #region Generic Titles

    [Fact]
    public void ARealEpisodeTitleInAnyLanguageBeatsAGenericOne()
    {
        IReadOnlyList<ITitle> titles =
        [
            Title("Episode 3", _tmdb, TitleLanguage.English),
            Title("Folge 3", _tmdb, TitleLanguage.German),
            Title("Duel", _anidb, TitleLanguage.Japanese),
        ];

        Assert.Equal("Duel", TextChooser.ChooseTitle(titles, Choice([TitleLanguage.English, TitleLanguage.German, TitleLanguage.Japanese], episode: true))?.Value);
        Assert.Equal("Episode 3", TextChooser.ChooseTitle(titles.Take(2).ToList(), Choice([TitleLanguage.English, TitleLanguage.German], episode: true))?.Value);
    }

    [Fact]
    public void ANumberedLabelIsARealTitle()
    {
        IReadOnlyList<ITitle> titles =
        [
            Title("Opening 2", _anidb, TitleLanguage.English),
            Title("Duel", _anidb, TitleLanguage.Japanese),
        ];

        Assert.Equal("Opening 2", TextChooser.ChooseTitle(titles, Choice([TitleLanguage.English, TitleLanguage.Japanese], episode: true))?.Value);
    }

    [Fact]
    public void ASeriesTitleIsNeverRankedAsGeneric()
    {
        IReadOnlyList<ITitle> titles =
        [
            Title("Episode 3", _tmdb, TitleLanguage.English),
            Title("Real", _anidb, TitleLanguage.Japanese),
        ];

        Assert.Equal("Episode 3", TextChooser.ChooseTitle(titles, Choice([TitleLanguage.English, TitleLanguage.Japanese]))?.Value);
    }

    [Fact]
    public void TheDefaultComesBeforeAMadeUpTitle()
    {
        var fallback = Title("Default", _anidb, TitleLanguage.Japanese);

        Assert.Equal("Default", TextChooser.ChooseTitle([], Choice([TitleLanguage.English], episode: true, fallback: fallback, synthesize: true))?.Value);

        var madeUp = TextChooser.ChooseTitle([], Choice([TitleLanguage.German, TitleLanguage.English], episode: true, synthesize: true));
        Assert.NotNull(madeUp);
        Assert.True(madeUp.IsSynthesized);
        Assert.Equal("Folge 5", madeUp.Value);
        Assert.Equal(MetadataSource.Generated, madeUp.Source);
    }

    [Fact]
    public void AMadeUpCandidateIsNeverChosenFromTheCandidates()
    {
        var madeUp = GenericEpisodeTitles.Synthesize(EpisodeType.Episode, 1, [TitleLanguage.English]);

        Assert.Null(TextChooser.ChooseTitle([madeUp], Choice([TitleLanguage.English], episode: true)));
    }

    #endregion

    #region AniDB's Own Titles

    private static MetadataGuid AnidbID(MetadataEntityType type)
        => new(MetadataSource.AniDB, type, "1");

    [Theory]
    [InlineData(TitleLanguage.Japanese, "日本語")]
    [InlineData(TitleLanguage.Romaji, "Romaji")]
    [InlineData(TitleLanguage.German, "Deutsch")]
    public void AnAniDBEpisodesFirstTitleInAHigherLanguageBeatsItsEnglishOne(TitleLanguage first, string expected)
    {
        ITitle[] titles =
        [
            Title("Real English", _anidb, TitleLanguage.English, TitleType.Main),
            Title("日本語", _anidb, TitleLanguage.Japanese, TitleType.None),
            Title("Romaji", _anidb, TitleLanguage.Romaji, TitleType.None),
            Title("Deutsch", _anidb, TitleLanguage.German, TitleType.None),
        ];
        var rule = MetadataTextManager.OwnTitleRuleOf(AnidbID(MetadataEntityType.Episode));

        foreach (var order in new MetadataSource[][] { [_tmdb, _anidb, MetadataSource.User], [_anidb, _tmdb, MetadataSource.User], [_tmdb, MetadataSource.User] })
            foreach (var synonyms in new[] { false, true })
                Assert.Equal(expected, TextChooser.ChooseStoredTitle(titles, _anidb, new([first, TitleLanguage.English], order, synonyms, true, OwnRule: rule))?.Value);
    }

    [Theory]
    [InlineData("Episode 5", TitleLanguage.Japanese, TitleLanguage.English)]
    [InlineData("Episode 13", TitleLanguage.English, TitleLanguage.Romaji)]
    public void AnAniDBEpisodesFirstTitleInAHigherLanguageAnswersEvenWhenItLooksGeneric(string generic, TitleLanguage higher, TitleLanguage lower)
    {
        ITitle[] titles =
        [
            Title(generic, _anidb, higher, TitleType.Main),
            Title("Real", _anidb, lower, TitleType.Main),
        ];
        var rule = MetadataTextManager.OwnTitleRuleOf(AnidbID(MetadataEntityType.Episode));

        foreach (var order in new MetadataSource[][] { [_tmdb, _anidb, MetadataSource.User], [_anidb, _tmdb, MetadataSource.User], [_tmdb, MetadataSource.User] })
            Assert.Equal(generic, TextChooser.ChooseStoredTitle(titles, _anidb, new([higher, lower], order, false, true, OwnRule: rule))?.Value);
    }

    [Fact]
    public void AnAniDBEpisodesFirstTitleInALanguageAnswersEvenWhenItLooksGeneric()
    {
        ITitle[] titles =
        [
            Title("Episode 25", _anidb, TitleLanguage.English, TitleType.Main),
            Title("Secret of the Head", _anidb, TitleLanguage.English, TitleType.Main),
        ];
        var rule = MetadataTextManager.OwnTitleRuleOf(AnidbID(MetadataEntityType.Episode));

        foreach (var order in new MetadataSource[][] { [_tmdb, _anidb, MetadataSource.User], [_anidb, _tmdb, MetadataSource.User], [_tmdb, MetadataSource.User] })
            foreach (var language in new[] { TitleLanguage.English, TitleLanguage.Main })
                foreach (var synonyms in new[] { false, true })
                    Assert.Equal("Episode 25", TextChooser.ChooseStoredTitle(titles, _anidb, new([language], order, synonyms, true, OwnRule: rule))?.Value);
    }

    [Fact]
    public void AUsersGenericTitleOnAnAniDBEpisodeStillComesAfterEveryRealOne()
    {
        ITitle[] titles =
        [
            Title("Episode 7", MetadataSource.User, TitleLanguage.English, TitleType.Main),
            Title("Romaji", _anidb, TitleLanguage.Romaji, TitleType.Main),
        ];
        var rule = MetadataTextManager.OwnTitleRuleOf(AnidbID(MetadataEntityType.Episode));

        Assert.Equal("Romaji", TextChooser.ChooseStoredTitle(titles, _anidb, new([TitleLanguage.English, TitleLanguage.Romaji], [_tmdb, _anidb, MetadataSource.User], false, true, OwnRule: rule))?.Value);
    }

    [Fact]
    public void APluginEpisodesRealTitlesStillComeBeforeItsGenericOnes()
    {
        var plugin = TestSources.Plugin;
        ITitle[] titles =
        [
            Title("Episode 25", plugin, TitleLanguage.English, TitleType.Main),
            Title("Secret of the Head", plugin, TitleLanguage.English, TitleType.Main),
            Title("Episode 13", plugin, TitleLanguage.German, TitleType.Main),
            Title("Deutsch", plugin, TitleLanguage.German),
        ];
        var rule = MetadataTextManager.OwnTitleRuleOf(new(plugin, MetadataEntityType.Episode, "1"));

        Assert.Equal(OwnTitleRule.AnyInLanguage, rule);
        foreach (var order in new MetadataSource[][] { [plugin, _tmdb, MetadataSource.User], [_tmdb, MetadataSource.User] })
        {
            Assert.Equal("Secret of the Head", TextChooser.ChooseStoredTitle(titles, plugin, new([TitleLanguage.English, TitleLanguage.German], order, false, true, OwnRule: rule))?.Value);
            Assert.Equal("Deutsch", TextChooser.ChooseStoredTitle([titles[0], titles[2], titles[3]], plugin, new([TitleLanguage.English, TitleLanguage.German], order, false, true, OwnRule: rule))?.Value);
        }
    }

    [Fact]
    public void AnAniDBAnimesEnglishSynonymIsNotChosenWithoutSynonymsButAnOfficialTitleIs()
    {
        ITitle[] titles =
        [
            Title("Romaji", _anidb, TitleLanguage.Romaji, TitleType.Main),
            Title("English Syn", _anidb, TitleLanguage.English, TitleType.Synonym),
            Title("English Short", _anidb, TitleLanguage.English, TitleType.Short),
        ];
        var rule = MetadataTextManager.OwnTitleRuleOf(AnidbID(MetadataEntityType.Series));

        foreach (var order in new MetadataSource[][] { [_anidb, _tmdb, MetadataSource.User], [_tmdb, _anidb, MetadataSource.User], [_tmdb, MetadataSource.User] })
        {
            Assert.Null(TextChooser.ChooseStoredTitle(titles, _anidb, new([TitleLanguage.English], order, false, false, OwnRule: rule)));
            Assert.Equal("English Syn", TextChooser.ChooseStoredTitle(titles, _anidb, new([TitleLanguage.English], order, true, false, OwnRule: rule))?.Value);

            // An official title answers without them.
            Assert.Equal("English", TextChooser.ChooseStoredTitle([.. titles, Title("English", _anidb, TitleLanguage.English, TitleType.Official)], _anidb,
                new([TitleLanguage.English], order, false, false, OwnRule: rule))?.Value);
        }
    }

    [Fact]
    public void APluginEntrysOwnSynonymStillAnswersWithoutSynonyms()
    {
        var plugin = TestSources.Plugin;
        ITitle[] titles = [Title("English Syn", plugin, TitleLanguage.English, TitleType.Synonym)];
        var rule = MetadataTextManager.OwnTitleRuleOf(new(plugin, MetadataEntityType.Series, "1"));

        Assert.Equal(OwnTitleRule.AnyInLanguage, rule);
        Assert.Equal("English Syn", TextChooser.ChooseStoredTitle(titles, plugin, new([TitleLanguage.English], [plugin, _tmdb, MetadataSource.User], false, false, OwnRule: rule))?.Value);
    }

    #endregion

    #region Overviews

    [Fact]
    public void AnOverviewIsChosenByPickThenLanguageThenSourceThenTheFallback()
    {
        IReadOnlyList<IText> overviews =
        [
            Title("TMDB", _tmdb, TitleLanguage.English),
            Title("AniDB", _anidb, TitleLanguage.English),
            Title("Picked", _anidb, TitleLanguage.Japanese, preference: TextPreference.Language),
        ];
        var fallback = Title("Fallback", _anidb, TitleLanguage.German);

        Assert.Equal("TMDB", TextChooser.ChooseOverview(overviews, [TitleLanguage.English], [_tmdb, _anidb], fallback)?.Value);
        Assert.Equal("AniDB", TextChooser.ChooseOverview(overviews, [TitleLanguage.English], [_anidb, _tmdb], fallback)?.Value);
        Assert.Equal("Picked", TextChooser.ChooseOverview(overviews, [TitleLanguage.Japanese, TitleLanguage.English], [_tmdb, _anidb], fallback)?.Value);
        Assert.Equal("Fallback", TextChooser.ChooseOverview(overviews, [TitleLanguage.German], [_tmdb, _anidb], fallback)?.Value);
    }

    #endregion
}
