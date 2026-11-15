using Shoko.Server.Models.Release;
using Xunit;

namespace Shoko.Tests.Models;

/// <summary>
///   A stored release's cross-references read back whatever number format
///   the database wrote: MySQL and MariaDB rewrote the percentages as floats.
/// </summary>
public class StoredReleaseCrossReferenceReadTests
{
    [Theory]
    [InlineData("""[{"IDs":["anidb://episode/1","anidb://series/2"],"PercentageStart":0.0,"PercentageEnd":100.0}]""", 0, 100)]
    [InlineData("""[{"IDs":["anidb://episode/1"],"PercentageStart":50,"PercentageEnd":100}]""", 50, 100)]
    public void ThePercentagesReadBackAsWholeNumbers(string json, int start, int end)
    {
        var release = new StoredReleaseInfo { EmbeddedCrossReferences = json };

        var crossReference = Assert.Single(release.CrossReferences);
        Assert.Equal((start, end), (crossReference.PercentageStart, crossReference.PercentageEnd));
    }
}
