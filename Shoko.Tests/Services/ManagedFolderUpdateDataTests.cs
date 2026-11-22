using Shoko.Abstractions.Video;
using Xunit;

namespace Shoko.Tests.Services;

public class ManagedFolderUpdateDataTests
{
    [Fact]
    public void IsNetworkShare_CountsAsSetWhenSetToNull()
    {
        var untouched = new ManagedFolderUpdateData();
        var reset = new ManagedFolderUpdateData { IsNetworkShare = null };

        Assert.False(untouched.HasIsNetworkShare);
        Assert.True(reset.HasIsNetworkShare);
        Assert.Null(reset.IsNetworkShare);
    }
}
