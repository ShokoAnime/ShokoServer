using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Shoko.Server.AniDB_API;
using Shoko.Server.Server;

namespace Shoko.Server.API.SignalR
{
    public class LegacyAniDBHub : Hub
    {
        private AniDBHelper AniDBHelper { get; set; }
        private LegacyAniDBEmitter AniDBEmitter { get; set; }

        public LegacyAniDBHub(AniDBHelper helper, LegacyAniDBEmitter emitter)
        {
            AniDBHelper = helper;
            AniDBEmitter = emitter;
        }

        public override async Task OnConnectedAsync()
        {
            if (ServerState.Instance.DatabaseAvailable)
                await AniDBEmitter.OnConnectedAsync(Clients.Caller);
        }
    }
}
