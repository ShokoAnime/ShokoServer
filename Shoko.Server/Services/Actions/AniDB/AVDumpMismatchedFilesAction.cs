using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;

namespace Shoko.Server.Services.Actions.AniDB;

/// <summary>
///   Queue AVDump jobs for files whose media info and AniDB data are
///   mismatched (e.g., chapter states differ).
/// </summary>
internal sealed class AVDumpMismatchedFilesAction(ActionService actionService) : IExecutableGlobalSystemAction
{
    public string Name => "AVDump Mismatched Files";
    public string? Description => "Queue AVDump jobs for files whose local media info and AniDB data are mismatched.";
    public ActionCategory Category => ActionCategory.AniDB;

    public Task Execute(CancellationToken cancellationToken = default)
        => actionService.AVDumpMismatchedFiles();
}
