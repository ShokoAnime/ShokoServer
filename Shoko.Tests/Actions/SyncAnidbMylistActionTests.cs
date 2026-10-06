using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Models;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Server.Actions;
using Shoko.Server.API.v3.Controllers;
using Shoko.Server.Services;
using Xunit;

namespace Shoko.Tests.Actions;

/// <summary>
/// The executable MyList sync hands its options to the queued sync, forcing a
/// fresh download unless told not to, and the legacy route still forces one.
/// </summary>
public class SyncAnidbMylistActionTests
{
    #region Helpers

    /// <summary>
    /// A MyList service that keeps the options of every scheduled full sync.
    /// </summary>
    /// <param name="scheduled">Receives the options.</param>
    /// <returns>The service.</returns>
    private static IMylistService Mylist(List<MylistSyncOptions?> scheduled)
    {
        var mylist = new Mock<IMylistService>();
        mylist.Setup(service => service.ScheduleSync(It.IsAny<MylistSyncOptions?>(), It.IsAny<bool>()))
            .Callback((MylistSyncOptions? options, bool _) => scheduled.Add(options))
            .Returns(Task.CompletedTask);
        return mylist.Object;
    }

    #endregion

    #region Tests

    [Fact]
    public async Task ByDefault_TheSyncDownloadsAFreshMylist()
    {
        var scheduled = new List<MylistSyncOptions?>();

        await new SyncAnidbMylistAction(Mylist(scheduled)).Execute(TestContext.Current.CancellationToken);

        Assert.Equal([new MylistSyncOptions { FetchMode = MylistFetchMode.IgnoreTimeCheck }], scheduled);
    }

    [Fact]
    public async Task WithoutForce_TheSyncFetchesAsTheSettingsSay_AndTakesTheWatchedStateOverrides()
    {
        var scheduled = new List<MylistSyncOptions?>();
        var action = new SyncAnidbMylistAction(Mylist(scheduled));
        ActionService.PopulateParameters(
            action,
            new Dictionary<string, object?> { ["Force"] = false, ["ReadWatched"] = true, ["SetUnwatched"] = false }
        );

        await action.Execute(TestContext.Current.CancellationToken);

        Assert.Equal([new MylistSyncOptions { ReadWatched = true, SetUnwatched = false }], scheduled);
    }

    [Fact]
    public async Task TheLegacyRoute_StillForcesAFreshMylist()
    {
        var scheduled = new List<MylistSyncOptions?>();
#pragma warning disable CS0618
        var controller = new LegacyActionController(
            null!,
            null!,
            null!,
            Mylist(scheduled),
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!
        );
#pragma warning restore CS0618

        await controller.SyncMylist();

        Assert.Equal([new MylistSyncOptions { FetchMode = MylistFetchMode.IgnoreTimeCheck }], scheduled);
    }

    #endregion
}
