using Shoko.Server.Extensions;
using Xunit;

namespace Shoko.Tests.Extensions;

/// <summary>
/// Covers the sort name a name sorts and is filed by in the filters.
/// </summary>
public class SortNameTests
{
    [Theory]
    [InlineData("The Melancholy of Haruhi Suzumiya", "Melancholy of Haruhi Suzumiya")]
    [InlineData("the melancholy of haruhi suzumiya", "melancholy of haruhi suzumiya")]
    [InlineData("An Archdemon's Dilemma", "Archdemon's Dilemma")]
    [InlineData("A-ko", "A-ko")]
    [InlineData("Theatre of Darkness", "Theatre of Darkness")]
    [InlineData("A", "A")]
    public void ASortNameLeavesOutALeadingArticle(string name, string expected)
        => Assert.Equal(expected, name.ToSortName());

    [Theory]
    [InlineData("Gekijouban Kara no Kyoukai", "Kara no Kyoukai")]
    [InlineData("Gekijō-ban Fate/stay night", "Fate/stay night")]
    [InlineData("Gekijouban: Kara no Kyoukai", "Kara no Kyoukai")]
    [InlineData("Gekijouban The Garden of Sinners", "Garden of Sinners")]
    [InlineData("劇場版 空の境界", "空の境界")]
    [InlineData("劇場版空の境界", "空の境界")]
    [InlineData("Gekijouban", "Gekijouban")]
    [InlineData("Gekijoubanzai", "Gekijoubanzai")]
    public void ASortNameLeavesOutALeadingMovieMarker(string name, string expected)
        => Assert.Equal(expected, name.ToSortName());

    [Theory]
    [InlineData(".hack//Sign", "hack//Sign")]
    [InlineData("\"Oshi no Ko\"", "Oshi no Ko\"")]
    [InlineData("【Oshi no Ko】", "Oshi no Ko】")]
    [InlineData("「Fate/Zero」", "Fate/Zero」")]
    [InlineData("“Bungaku Shoujo” Movie", "Bungaku Shoujo” Movie")]
    [InlineData("...Ichika", "Ichika")]
    [InlineData("+Anima", "+Anima")]
    [InlineData("\"\"", "\"\"")]
    public void ASortNameLeavesOutLeadingPunctuationButNotSymbols(string name, string expected)
        => Assert.Equal(expected, name.ToSortName());

    [Theory]
    [InlineData("Ōkami Kakushi", "Okami Kakushi")]
    [InlineData("Élan", "Elan")]
    [InlineData("Straße", "Strasse")]
    [InlineData("Ｆａｔｅ", "Fate")]
    [InlineData("がっこうぐらし!", "がっこうぐらし!")]
    public void ASortNameFoldsLatinAccentsAndFullWidthFormsOnly(string name, string expected)
        => Assert.Equal(expected, name.ToSortName());
}
