using System.Collections.Generic;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Scheduling.Jobs.Metadata;
using Xunit;

namespace Shoko.Tests.Scheduling.Jobs.Metadata;

/// <summary>
/// How a metadata job shows its entry in the queue: as the entry's source, kind and ID in their display form.
/// </summary>
public class MetadataJobDetailsTests
{
    [Fact]
    public void AnEntryIsShownAsItsSourceKindAndID()
    {
        var details = new Dictionary<string, object>().WithEntry("tmdb://series/21985");

        Assert.Equal(MetadataSource.TMDB.Name, details["Source"]);
        Assert.Equal(MetadataEntityType.Series.Name, details["Kind"]);
        Assert.Equal("21985", details["ID"]);
        Assert.False(details.ContainsKey("Entry"));
    }

    [Fact]
    public void TextThatIsNoEntryIsShownAsItIs()
    {
        var details = new Dictionary<string, object>().WithEntry("not an entry");

        Assert.Equal("not an entry", details["Entry"]);
        Assert.False(details.ContainsKey("Source"));
    }
}
