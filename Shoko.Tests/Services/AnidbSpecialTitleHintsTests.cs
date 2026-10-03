using System.Linq;
using Shoko.Server.Services.Ordering;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers <see cref="AnidbSpecialTitleHints"/>: which AniDB special titles
/// say where the special airs, and the order of specials after one episode.
/// </summary>
public class AnidbSpecialTitleHintsTests
{
    #region Parsing

    [Theory]
    [InlineData("Episode 17.5", 24, 17, 5)]
    [InlineData("Ep 6.5", 12, 6, 5)]
    [InlineData("ep12.5", 12, 12, 5)]
    [InlineData("Episode 0.5", 12, 0, 5)]
    [InlineData("Episode 0.8", 12, 0, 8)]
    [InlineData("Kaichou wa Maid-sama! (Unaired Episode 8.5)", 26, 8, 5)]
    [InlineData("Omake (ep.17.5)", 25, 17, 5)]
    [InlineData("Episode 100.1", 112, 100, 1)]
    [InlineData("Episode 11.5 - The Day Before", 12, 11, 5)]
    [InlineData("Episode 2.5: Interlude", 12, 2, 5)]
    [InlineData("EPSIODE 4.5", 12, 4, 5)]
    [InlineData("#7.5", 12, 7, 5)]
    [InlineData("Audio Drama Episode 13.13: The Last Call", 13, 13, 13)]
    [InlineData("Preview Episode 20.5", 24, 20, 5)]
    [InlineData("Episode 0", 12, 0, 0)]
    [InlineData("Episode 00", 12, 0, 0)]
    [InlineData("ep0", 12, 0, 0)]
    public void AnEnglishTitleWithAnEpisodeWordAndANumberWithADotPlacesTheSpecial(string title, int regularEpisodes, int afterEpisode, int sortKey)
    {
        Assert.Equal(new AnidbSpecialTitleHint(afterEpisode, sortKey), AnidbSpecialTitleHints.Parse(title, null, null, regularEpisodes));
        Assert.Equal(new AnidbSpecialTitleHint(afterEpisode, sortKey), AnidbSpecialTitleHints.Parse(null, title, null, regularEpisodes));
    }

    [Theory]
    [InlineData("Episode 6.5", 3)]
    [InlineData("Episode 9.5", 3)]
    [InlineData("Episode 13.5", 12)]
    [InlineData("Stage 17.5: Truth Behind the Mask", 24)]
    [InlineData("Selepro-chan! Theater 7.5", 12)]
    [InlineData("Talk 6.2", 12)]
    [InlineData("Volume 2.5", 12)]
    [InlineData("Ver.0.5", 12)]
    [InlineData("2009.12.26", 2010)]
    [InlineData("Evangelion: 3.0", 12)]
    [InlineData("Recap", 12)]
    [InlineData("Recap Episode", 12)]
    [InlineData("Episode 5", 12)]
    [InlineData("Episode 12", 12)]
    [InlineData("Episode 0.5.1", 12)]
    [InlineData("Prep.1.5", 12)]
    [InlineData("", 12)]
    public void AnEnglishTitleWithoutAnEpisodeNumberInRangePlacesNothing(string title, int regularEpisodes)
        => Assert.Null(AnidbSpecialTitleHints.Parse(title, title, null, regularEpisodes));

    [Theory]
    [InlineData("第17.5話", 24, 17, 5)]
    [InlineData("第１７．５話", 24, 17, 5)]
    [InlineData("特別編 第6.5回", 12, 6, 5)]
    [InlineData("第0話", 12, 0, 0)]
    public void AJapaneseTitleWithANumberWithADotPlacesTheSpecial(string title, int regularEpisodes, int afterEpisode, int sortKey)
        => Assert.Equal(new AnidbSpecialTitleHint(afterEpisode, sortKey), AnidbSpecialTitleHints.Parse(null, null, title, regularEpisodes));

    [Theory]
    [InlineData("第6.5話", 3)]
    [InlineData("第5話", 12)]
    [InlineData("劇場版 3.0", 12)]
    public void AJapaneseTitleWithoutAnEpisodeNumberInRangePlacesNothing(string title, int regularEpisodes)
        => Assert.Null(AnidbSpecialTitleHints.Parse(null, null, title, regularEpisodes));

    [Fact]
    public void TheEnglishTitleIsReadBeforeTheOthers()
        => Assert.Equal(new AnidbSpecialTitleHint(3, 5), AnidbSpecialTitleHints.Parse("Episode 3.5", "Episode 4.5", "第5.5話", 12));

    [Fact]
    public void ATitleOutOfRangeFallsBackToTheNextTitle()
        => Assert.Equal(new AnidbSpecialTitleHint(2, 5), AnidbSpecialTitleHints.Parse("Episode 30.5", "Special", "第2.5話", 12));

    #endregion

    #region Airing Order

    [Fact]
    public void SpecialsAfterTheSameEpisodeSortByTheirKeyThenTheirSpecialNumber()
    {
        var specials = new[]
        {
            (Number: 4, Hint: new AnidbSpecialTitleHint(6, 5)),
            (Number: 2, Hint: new AnidbSpecialTitleHint(2, 5)),
            (Number: 3, Hint: new AnidbSpecialTitleHint(6, 2)),
            (Number: 1, Hint: new AnidbSpecialTitleHint(6, 5)),
            (Number: 5, Hint: new AnidbSpecialTitleHint(0, 0)),
        };

        Assert.Equal([5, 2, 3, 1, 4], AnidbSpecialTitleHints.InAiringOrder(specials, special => special.Hint, special => special.Number).Select(special => special.Number));
    }

    #endregion
}
