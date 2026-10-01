using Shoko.Server.Filters;
using Xunit;

namespace Shoko.Tests.Filters;

/// <summary>
/// Covers the (source, name) pairs offered for the source tag and genre expressions.
/// </summary>
public class ExpressionDiscoveryTests
{
    [Fact]
    public void SourceTagPairs_GroupsBySourceInOrder_OnceEachIgnoringCase()
    {
        (string, string)[] tags =
        [
            ("tmdb", "Drama"),
            ("plugin", "action"),
            ("tmdb", "drama"),
            ("tmdb", "Comedy"),
            ("anidb", "Action"),
            ("plugin", ""),
        ];

        var pairs = ExpressionDiscovery.SourceTagPairs(tags, ["plugin", "tmdb"]);

        string[][] expected = [["plugin", "action"], ["tmdb", "Comedy"], ["tmdb", "Drama"]];
        Assert.Equal(expected, pairs);
    }
}
