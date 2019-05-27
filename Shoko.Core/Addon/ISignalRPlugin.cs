using Microsoft.AspNetCore.SignalR;

namespace Shoko.Core.Addon
{
    public interface ISignalRPlugin
    {
        void RegisterSignalR(HubRouteBuilder routes);
    }
}
