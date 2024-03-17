using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Shoko.Server.API.SignalR.Aggregate;

public abstract class BaseEmitter : IEmitter
{
    protected readonly IHubContext<Hub> Hub;
    private string _group;
    public string Group => _group ??= GetType().FullName?.Split('.').LastOrDefault()?.Replace("Emitter", "") ?? "Misc";

    protected BaseEmitter(IHubContext<Hub> hub)
    {
        Hub = hub;
    }

    public abstract object GetInitialMessage();

    public async Task SendAsync(string message, params object[] args)
    {
        await Hub.Clients.Group(Group).SendCoreAsync(GetName(message), args);
    }

    public string GetName(string message)
    {
        return Group + ":" + message;
    }
}
