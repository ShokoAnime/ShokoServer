using Shoko.Abstractions.Metadata;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers the overview a user gives a group, which a blank value keeps rather than removes, so the
/// group can show no overview at all instead of its main series' one.
/// </summary>
public class CustomOverviewTests
{
    private static readonly MetadataGuid Group = new(MetadataSource.Shoko, MetadataEntityType.Collection, "7");

    private static MetadataTextManager Manager()
        => TestTextManager.Build(new MetadataTextStore(new TextCache(), new CacheOnlyRowWriter()));

    [Fact]
    public void ABlankOverviewIsKeptUntilItIsRemoved()
    {
        var manager = Manager();

        Assert.True(manager.SetCustomOverview(Group, "Told by the user."));
        Assert.True(manager.SetCustomOverview(Group, string.Empty));
        Assert.False(manager.SetCustomOverview(Group, string.Empty));
        Assert.Equal(string.Empty, manager.CustomOverviewOf(Group)?.Value);
        Assert.Single(manager.GetOverviews(Group, new() { Source = MetadataSource.User }));

        Assert.True(manager.SetCustomOverview(Group, null));
        Assert.False(manager.SetCustomOverview(Group, null));
        Assert.Null(manager.CustomOverviewOf(Group));

        // A blank overview is stored as the user's also when there was none.
        Assert.True(manager.SetCustomOverview(Group, "  "));
        Assert.Equal(("  ", MetadataSource.User), (manager.CustomOverviewOf(Group)?.Value, manager.CustomOverviewOf(Group)?.Source));
    }
}
