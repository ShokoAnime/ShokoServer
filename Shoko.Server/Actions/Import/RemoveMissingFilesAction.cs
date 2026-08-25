using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Remove entries in the Shoko database for files that are no longer
///   accessible, optionally removing them from the user's AniDB MyList too.
/// </summary>
public sealed class RemoveMissingFilesAction(ActionService actionService) : IExecutableAction
{
    /// <summary>
    ///   Whether to remove the files from the user's AniDB MyList as well as
    ///   from the local database. The configured delete type still decides what
    ///   removal means, so <c>DeleteLocalOnly</c> leaves the MyList alone
    ///   regardless.
    /// </summary>
    [Display(Name = "Remove MyList")]
    public bool RemoveMylist { get; set; } = true;

    public string Name => "Remove Missing Files";

    public string? Description => "Remove entries in the Shoko database for files that are no longer accessible.";

    public ActionCategory Category => ActionCategory.Import;

    public ActionPermission Permission => ActionPermission.User;

    public Task Execute(CancellationToken token = default)
        => actionService.RemoveRecordsWithoutPhysicalFiles(RemoveMylist);
}
