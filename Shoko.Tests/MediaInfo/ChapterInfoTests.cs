using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Video.Media;
using Shoko.Server.MediaInfo;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.MediaInfo;

/// <summary>
/// Covers how a chapter's titles are read from what MediaInfo writes for a
/// menu entry, and how the chapter answers as a titled entry.
/// </summary>
public class ChapterInfoTests
{
    #region Helpers

    /// <summary>
    /// The titles read from a value, as (value, language, code, type).
    /// </summary>
    private static List<(string Value, TitleLanguage Language, string Code, TitleType Type)> Read(string? value)
        => ChapterInfo.ParseTitles(value).Select(title => (title.Value, title.Language, title.LanguageCode, title.Type)).ToList();

    /// <summary>
    /// Runs an action and gathers the language strings reported as unknown
    /// while it ran.
    /// </summary>
    private static List<string> Reported(Action action)
    {
        var reported = new List<string>();
        void OnUnknown(string language)
        {
            lock (reported)
                reported.Add(language);
        }

        LanguageExtensions.OnUnknownLanguage += OnUnknown;
        try
        {
            action();
        }
        finally
        {
            LanguageExtensions.OnUnknownLanguage -= OnUnknown;
        }

        return reported;
    }

    #endregion

    #region Parsing

    [Theory]
    [InlineData("Part 1: The Beginning")]
    [InlineData("OP: Snow Halation")]
    [InlineData("Chapter 01")]
    [InlineData("Intro")]
    public void PlainName_IsOneTitleWithoutLanguage(string value)
    {
        var reported = Reported(() => Assert.Equal([(value, TitleLanguage.Unknown, "unk", TitleType.Main)], Read(value)));

        Assert.DoesNotContain(reported, language => value.StartsWith(language, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("en:Opening", "Opening", TitleLanguage.English, "en")]
    [InlineData("jpn:オープニング", "オープニング", TitleLanguage.Japanese, "jpn")]
    [InlineData("en:Part 1: The Beginning", "Part 1: The Beginning", TitleLanguage.English, "en")]
    public void Prefix_IsTheLanguage(string input, string value, TitleLanguage language, string code)
        => Assert.Equal([(value, language, code, TitleType.Main)], Read(input));

    [Fact]
    public void LanguageTag_KeepsItsRegion()
    {
        var title = Assert.Single(ChapterInfo.ParseTitles("pt-BR:Abertura"));

        Assert.Equal(("Abertura", TitleLanguage.BrazilianPortuguese, "pt", "BR"), (title.Value, title.Language, title.LanguageCode, title.CountryCode));
    }

    [Fact]
    public void SeveralLanguages_AreSplitInFileOrder()
        => Assert.Equal(
            [
                ("Opening", TitleLanguage.English, "en", TitleType.Main),
                ("オープニング", TitleLanguage.Japanese, "ja", TitleType.Official),
                ("Ouverture", TitleLanguage.French, "fr", TitleType.Official),
            ],
            Read("en:Opening - ja:オープニング - fr:Ouverture")
        );

    [Theory]
    [InlineData("en:Part A - Intro", "Part A - Intro")]
    [InlineData("en:Part A - OP: Intro", "Part A - OP: Intro")]
    [InlineData("en:Side A - Part 2: Return", "Side A - Part 2: Return")]
    public void DashWithoutLanguage_StaysInTheTitle(string value, string expected)
        => Assert.Equal([(expected, TitleLanguage.English, "en", TitleType.Main)], Read(value));

    [Fact]
    public void UndeterminedFirstName_HasNoLanguage()
        => Assert.Equal(
            [
                ("Opening", TitleLanguage.Unknown, "unk", TitleType.Main),
                ("オープニング", TitleLanguage.Japanese, "ja", TitleType.Official),
            ],
            Read("Opening - ja:オープニング")
        );

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("en:")]
    [InlineData("en:  - ja:")]
    public void EmptyNames_AreLeftOut(string? value)
        => Assert.Empty(Read(value));

    [Fact]
    public void EmptyName_IsSkippedAmongOthers()
        => Assert.Equal([("オープニング", TitleLanguage.Japanese, "ja", TitleType.Main)], Read("en: - ja: オープニング "));

    [Fact]
    public void LaterNameInSameLanguage_IsLeftOut()
        => Assert.Equal([("Opening", TitleLanguage.English, "en", TitleType.Main)], Read("en:Opening - en:Opening Theme"));

    [Fact]
    public void UnknownLanguageCode_IsReportedOnce()
    {
        // "qqa" sits in ISO 639's range for local use, so nothing maps it.
        List<(string, TitleLanguage, string, TitleType)>? first = null;
        var reported = Reported(() =>
        {
            first = Read("qqa:Opening");
            Read("qqa:Ending - en:Ending");
        });

        Assert.Equal([("Opening", TitleLanguage.Unknown, "qqa", TitleType.Main)], first);
        Assert.Equal(["qqa"], reported.Where(language => language == "qqa"));
    }

    [Fact]
    public void UppercasePrefix_IsNotReported()
    {
        var reported = Reported(() => Read("QQB: Opening"));

        Assert.DoesNotContain("QQB", reported);
    }

    [Fact]
    public void Parse_ReadsTheStartTime()
    {
        var chapter = ChapterInfo.Parse("_00_22_10_079", "en:Ending");

        Assert.NotNull(chapter);
        Assert.Equal(new TimeSpan(0, 0, 22, 10, 79), chapter.Timestamp);
        Assert.Equal("Ending", Assert.Single(chapter.Titles).Value);
    }

    [Fact]
    public void Parse_ChapterWithoutNames_HasNoTitles()
    {
        // MediaInfo writes the start time as the value of a chapter with no name.
        var chapter = ChapterInfo.Parse("_00_01_23_456", "00:01:23.456");

        Assert.NotNull(chapter);
        Assert.Empty(chapter.Titles);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("_Chapters_Pos_Begin")]
    [InlineData("_aa_bb_cc_ddd")]
    public void Parse_SkipsEntriesThatAreNotChapters(string? key)
        => Assert.Null(ChapterInfo.Parse(key, "en:Opening"));

    [Fact]
    public void MediaContainer_ReadsTheFirstMenuOnly()
    {
        var container = new MediaContainer
        {
            media = new()
            {
                track =
                [
                    new MenuStream { extra = new() { ["_00_00_00_000"] = "en:Prologue - ja:プロローグ", ["_00_02_29_024"] = "Part 1: Opening" } },
                    new MenuStream { extra = new() { ["_00_00_00_000"] = "en:Other Edition" } },
                ],
            },
        };

        var chapters = ((IMediaInfo)container).Chapters;

        Assert.Equal(2, chapters.Count);
        Assert.Equal(["Prologue", "プロローグ"], chapters[0].Titles.Select(title => title.Value));
        Assert.Equal(TimeSpan.Zero, chapters[0].Timestamp);
        Assert.Equal("Part 1: Opening", Assert.Single(chapters[1].Titles).Value);
    }

    #endregion

    #region Titles

    [Fact]
    public void DefaultTitle_IsTheFirstName()
    {
        var chapter = new ChapterInfo(ChapterInfo.ParseTitles("ja:オープニング - en:Opening"), TimeSpan.Zero);

        Assert.Equal("オープニング", chapter.DefaultTitle.Value);
    }

    [Fact]
    public void DefaultTitle_WithoutNames_IsEmpty()
    {
        StubSettingsProvider.Install();
        var chapter = new ChapterInfo([], TimeSpan.Zero);

        Assert.Equal(string.Empty, chapter.DefaultTitle.Value);
        Assert.Null(chapter.PreferredTitle);
        Assert.Equal(string.Empty, chapter.Title);
    }

    [Theory]
    [InlineData(new[] { TitleLanguage.English }, "Opening")]
    [InlineData(new[] { TitleLanguage.Japanese, TitleLanguage.English }, "オープニング")]
    [InlineData(new[] { TitleLanguage.French, TitleLanguage.English }, "Opening")]
    [InlineData(new[] { TitleLanguage.Main }, "Prologue")]
    [InlineData(new[] { TitleLanguage.French }, null)]
    public void PreferredTitle_FollowsTheLanguageOrder(TitleLanguage[] languages, string? expected)
    {
        var titles = ChapterInfo.ParseTitles("Prologue - en:Opening - ja:オープニング");

        Assert.Equal(expected, ChapterInfo.ChoosePreferredTitle(titles, languages)?.Value);
    }

    [Fact]
    public void PreferredTitle_ReadsThreeLetterCodes()
    {
        var titles = ChapterInfo.ParseTitles("eng:Opening - jpn:オープニング");

        Assert.Equal("オープニング", ChapterInfo.ChoosePreferredTitle(titles, [TitleLanguage.Japanese])?.Value);
    }

    /// <summary>
    /// A chapter's title is its preferred one, in the default episode title order (English
    /// only), else its first.
    /// </summary>
    /// <param name="value">The chapter's names, as MediaInfo writes them.</param>
    /// <param name="expected">The title the chapter answers with.</param>
    [Theory]
    [InlineData("ja:オープニング - en:Opening", "Opening")]
    [InlineData("ja:オープニング - fr:Ouverture", "オープニング")]
    public void Title_IsThePreferredElseTheDefault(string value, string expected)
    {
        StubSettingsProvider.Install();
        var chapter = new ChapterInfo(ChapterInfo.ParseTitles(value), TimeSpan.Zero);

        Assert.Equal(expected, chapter.Title);
    }

    #endregion
}
