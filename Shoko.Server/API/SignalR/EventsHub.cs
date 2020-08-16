using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Shoko.Server.API.SignalR.Models;
using Shoko.Server.Server;

namespace Shoko.Server.API.SignalR
{
    public class EventsHub : Hub
    {
        private readonly EventEmitter _eventEmitter;

        public EventsHub(EventEmitter eventEmitter)
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
            if (ServerState.Instance.DatabaseAvailable)
                await Clients.Caller.SendAsync("CommandProcessingStatus", new Dictionary<string, object>
                {
                    {
                        "GeneralQueueState", new QueueStateSignalRModel
                        {
                            State = ShokoService.CmdProcessorGeneral.QueueState.queueState,
                            Description = ShokoService.CmdProcessorGeneral.QueueState.formatMessage()
                        }
                    },
                    {
                        "HasherQueueState", new QueueStateSignalRModel
                        {
                            State = ShokoService.CmdProcessorHasher.QueueState.queueState,
                            Description = ShokoService.CmdProcessorHasher.QueueState.formatMessage()
                        }
                    },
                    {
                        "ImageQueueState", new QueueStateSignalRModel
                        {
                            State = ShokoService.CmdProcessorImages.QueueState.queueState,
                            Description = ShokoService.CmdProcessorImages.QueueState.formatMessage()
                        }
                    },
                    {"GeneralQueueCount", ShokoService.CmdProcessorGeneral.QueueCount},
                    {"HasherQueueCount", ShokoService.CmdProcessorHasher.QueueCount},
                    {"ImageQueueCount", ShokoService.CmdProcessorImages.QueueCount},
                });
        }
    }
}
