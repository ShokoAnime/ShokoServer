using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Shoko.Abstractions.Web.Attributes;

namespace Shoko.Server.API.SignalR;

/// <summary>
///   Streams the server's logs to administrators, and in setup and failure
///   mode to anyone, so a start that went wrong can be followed.
/// </summary>
[Authorize(Roles = "admin,init")]
[InitFriendly]
[DatabaseBlockedExempt]
public class LoggingHub : Hub
{
    private readonly LoggingEmitter _loggingEmitter;

    public LoggingHub(LoggingEmitter emitter)
    {
        _loggingEmitter = emitter;
    }

    public override async Task OnConnectedAsync()
    {
        if (_loggingEmitter.Target is not { MaxLogsCount: > 0 } target)
        {
            return;
        }

        await Clients.Caller.SendAsync(target.ConnectMethodName, target.Logs.ToArray());
    }
}
