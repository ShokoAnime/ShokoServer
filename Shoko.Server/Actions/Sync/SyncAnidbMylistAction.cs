using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Models;
using Shoko.Abstractions.Metadata.Anidb.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Sync all local state to the AniDB MyList when a person asks for it, by
///   default with a freshly downloaded MyList. This can overwrite AniDB data.
///   <see cref="SyncAnidbMylistOnScheduleAction"/> is the scheduled form.
/// </summary>
/// <param name="mylistService">Queues the sync.</param>
public sealed class SyncAnidbMylistAction(IMylistService mylistService) : IExecutableAction
{
    #region Options

    /// <summary>
    ///   Download a fresh MyList from AniDB however recently one was
    ///   downloaded. Left off, the sync fetches as the MyList settings say,
    ///   reusing a MyList downloaded in the last few hours.
    /// </summary>
    public bool Force { get; set; } = true;

    /// <summary>
    ///   Import watched states from AniDB for older differences, or
    ///   <c>null</c> to do as the MyList settings say.
    /// </summary>
    public bool? ReadWatched { get; set; }

    /// <summary>
    ///   Import unwatched states from AniDB for older differences, or
    ///   <c>null</c> to do as the MyList settings say.
    /// </summary>
    public bool? ReadUnwatched { get; set; }

    /// <summary>
    ///   Export watched states to AniDB for older differences, or
    ///   <c>null</c> to do as the MyList settings say.
    /// </summary>
    public bool? SetWatched { get; set; }

    /// <summary>
    ///   Export unwatched states to AniDB for older differences, or
    ///   <c>null</c> to do as the MyList settings say.
    /// </summary>
    public bool? SetUnwatched { get; set; }

    #endregion

    #region Action

    public string Name => "Sync AniDB MyList";

    public string? Description
        => "Syncs all local state to the AniDB MyList, by default with a freshly downloaded MyList. This can overwrite AniDB data irreversibly.";

    public ActionCategory Category => ActionCategory.Sync;

    public ActionPermission Permission => ActionPermission.Admin;

    public bool RequiresConfirmation => true;

    public string? ConfirmationMessage => "Are you sure you want to sync local state with the AniDB MyList for all series? This may take a while.";

    public Task Execute(CancellationToken token = default)
        => mylistService.ScheduleSync(new MylistSyncOptions
        {
            FetchMode = Force ? MylistFetchMode.IgnoreTimeCheck : null,
            ReadWatched = ReadWatched,
            ReadUnwatched = ReadUnwatched,
            SetWatched = SetWatched,
            SetUnwatched = SetUnwatched,
        });

    #endregion
}
