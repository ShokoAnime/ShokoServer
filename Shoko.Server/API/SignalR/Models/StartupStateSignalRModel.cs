using Shoko.Server.API.v3.Models.Shoko;

namespace Shoko.Server.API.SignalR.Models;

/// <summary>
///   Where the start is, as the startup hub sends it: on connect, and whenever
///   it changes.
/// </summary>
public class StartupStateSignalRModel
{
    /// <summary>
    ///   The state, as <c>GET /api/v3/Init/Status</c> gives it: <c>Waiting</c>,
    ///   <c>Starting</c>, <c>Started</c> or <c>Failed</c>; started and failed are
    ///   the outcomes.
    /// </summary>
    public ServerStatus.StartupState State { get; }

    /// <summary>
    ///   What the start is doing while starting, or why it failed when failed;
    ///   otherwise <c>null</c>.
    /// </summary>
    public string? Message { get; }

    public StartupStateSignalRModel(ServerStatus.StartupState state, string? message)
    {
        State = state;
        Message = message;
    }
}
