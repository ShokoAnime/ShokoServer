using System.Collections.Generic;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Scheduling.Jobs.Metadata;
using Xunit;

namespace Shoko.Tests.Scheduling.Jobs.Metadata;

/// <summary>
/// How a metadata job shows its entry in the queue: as the entry's kind and ID, with its source unless the job's provider names it.
/// </summary>
public class MetadataJobDetailsTests
{
    [Fact]
    public void AnEntryIsShownAsItsKindAndID_WithItsSourceUnlessTheProviderNamesIt()
    {
        var details = new Dictionary<string, object>().WithEntry("tmdb://series/21985");
        var ownSource = new Dictionary<string, object>().WithEntry("tmdb://series/21985", MetadataSource.TMDB);

        Assert.Equal(MetadataSource.TMDB.Name, details["Source"]);
        Assert.Equal($"{MetadataEntityType.Series.Name} 21985", details["Entry"]);
        Assert.Equal(["Entry"], ownSource.Keys);
    }

    [Fact]
    public void TextThatIsNoEntryIsShownAsItIs()
    {
        var details = new Dictionary<string, object>().WithEntry("not an entry");

        Assert.Equal("not an entry", details["Entry"]);
        Assert.False(details.ContainsKey("Source"));
    }
}
