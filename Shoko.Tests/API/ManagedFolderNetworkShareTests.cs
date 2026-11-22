using Newtonsoft.Json;
using Shoko.Server.API.v3.Models.Shoko;
using Xunit;

namespace Shoko.Tests.API;

public class ManagedFolderNetworkShareTests
{
    [Theory]
    [InlineData("""{ "ID": 1, "Name": "Library", "Path": "/mnt/library/" }""", false, null)]
    [InlineData("""{ "ID": 1, "Name": "Library", "Path": "/mnt/library/", "IsNetworkShare": null }""", true, null)]
    [InlineData("""{ "ID": 1, "Name": "Library", "Path": "/mnt/library/", "IsNetworkShare": true }""", true, true)]
    public void IsNetworkShare_IsOnlyGivenWhenTheRequestHasIt(string json, bool expectedHas, bool? expectedValue)
    {
        var folder = JsonConvert.DeserializeObject<ManagedFolder>(json)!;

        Assert.Equal(expectedHas, folder.HasIsNetworkShare);
        Assert.Equal(expectedValue, folder.IsNetworkShare);
    }
}
