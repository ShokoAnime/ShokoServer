using Shoko.Server.Databases;
using Xunit;

namespace Shoko.Tests.Databases;

/// <summary>
/// The order the step copying AniDB's old anime titles gives them: the order of the anime's
/// <c>AllTitles</c> column, which each import rewrote in the order AniDB listed its titles.
/// </summary>
public class AnidbTitleOrderMigrationTests
{
    #region Ordering

    [Fact]
    public void TheTitlesFollowTheOrderOfTheAllTitlesColumnNotTheirIDs()
        => Assert.Equal(
            [2, 0, 3, 1],
            DatabaseFixes.OrderAnidbAnimeTitles(["Main", "English", "日本語", "Short"], "English|Short|Main|日本語")
        );

    [Fact]
    public void TitlesWithTheSameTextTakeItsPlacesInTheOrderOfTheirIDs()
        => Assert.Equal(
            [1, 0, 3, 2],
            DatabaseFixes.OrderAnidbAnimeTitles(["Same", "Main", "Same", "Other"], "Main|Same|Other|Same")
        );

    [Fact]
    public void TitlesTheColumnDoesNotHaveGoLastInTheOrderOfTheirIDs()
        => Assert.Equal(
            [3, 1, 2, 0, 4],
            DatabaseFixes.OrderAnidbAnimeTitles(["New", "Main", "Same", "English", "Same"], "English|Main|Same|Gone")
        );

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void WithoutTheColumnTheTitlesKeepTheOrderOfTheirIDs(string? allTitles)
        => Assert.Equal([0, 1, 2], DatabaseFixes.OrderAnidbAnimeTitles(["C", "A", "B"], allTitles));

    [Fact]
    public void BackticksAndApostrophesAreTheSameWhenCompared()
        => Assert.Equal([1, 0], DatabaseFixes.OrderAnidbAnimeTitles(["Kono `Anime`", "Main"], "Main|Kono 'Anime'"));

    [Fact]
    public void TheTextIsComparedExactly()
        => Assert.Equal([1, 0], DatabaseFixes.OrderAnidbAnimeTitles(["main", "Other"], "Main|Other"));

    #endregion
}
