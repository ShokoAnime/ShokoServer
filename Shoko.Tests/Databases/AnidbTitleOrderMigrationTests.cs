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

    [Theory]
    [InlineData(new[] { "Main", "English", "日本語", "Short" }, "English|Short|Main|日本語", new[] { 2, 0, 3, 1 })] // the column's order, not the IDs
    [InlineData(new[] { "Same", "Main", "Same", "Other" }, "Main|Same|Other|Same", new[] { 1, 0, 3, 2 })] // the same text takes its places by ID
    [InlineData(new[] { "New", "Main", "Same", "English", "Same" }, "English|Main|Same|Gone", new[] { 3, 1, 2, 0, 4 })] // titles not in the column go last
    [InlineData(new[] { "C", "A", "B" }, null, new[] { 0, 1, 2 })] // without the column the IDs decide
    [InlineData(new[] { "C", "A", "B" }, "", new[] { 0, 1, 2 })]
    [InlineData(new[] { "Kono `Anime`", "Main" }, "Main|Kono 'Anime'", new[] { 1, 0 })] // backticks and apostrophes compare the same
    [InlineData(new[] { "main", "Other" }, "Main|Other", new[] { 1, 0 })] // the text is compared exactly
    public void TheTitlesTakeTheOrderOfTheAllTitlesColumn(string[] titles, string? allTitles, int[] expected)
        => Assert.Equal(expected, DatabaseFixes.OrderAnidbAnimeTitles(titles, allTitles));

    #endregion
}
