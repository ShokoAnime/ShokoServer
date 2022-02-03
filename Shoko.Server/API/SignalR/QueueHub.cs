using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Shoko.Server.API.SignalR.Models;
using Shoko.Server.Server;

namespace Shoko.Server.API.SignalR
{
    public class QueueHub : Hub
    {
        private readonly QueueEmitter _eventEmitter;

        public QueueHub(QueueEmitter eventEmitter)
        {
            _eventEmitter = eventEmitter;
        }

        public void ChangeQueueProcessingState(string queue, bool paused)
        {
            switch (queue.ToLower())
            {
                case "general":
                    ShokoService.CmdProcessorGeneral.Paused = paused;
                    break;
                case "hasher":
                    ShokoService.CmdProcessorHasher.Paused = paused;
                    break;
                case "images":
                    ShokoService.CmdProcessorImages.Paused = paused;
                    break;
            }
        }

        public override async Task OnConnectedAsync()
        {
            await _eventEmitter.OnConnectedAsync(Clients.Caller);
        }
    }
}
