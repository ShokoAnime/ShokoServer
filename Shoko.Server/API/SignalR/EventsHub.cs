using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

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
                await Clients.Caller.SendAsync("CommandProcessingStatus", new {
                    General = new
                    {
                        State = ShokoService.CmdProcessorGeneral.QueueState.formatMessage(),
                        Count = ShokoService.CmdProcessorGeneral.QueueCount,
                        ShokoService.CmdProcessorGeneral.ProcessingCommands,
                        ShokoService.CmdProcessorGeneral.Paused
                    },

                    Hasher = new
                    {
                        State = ShokoService.CmdProcessorHasher.QueueState.formatMessage(),
                        Count = ShokoService.CmdProcessorHasher.QueueCount,
                        ShokoService.CmdProcessorHasher.ProcessingCommands,
                        ShokoService.CmdProcessorHasher.Paused
                    },

                    Images = new
                    {
                        State = ShokoService.CmdProcessorImages.QueueState.formatMessage(),
                        Count = ShokoService.CmdProcessorImages.QueueCount,
                        ShokoService.CmdProcessorImages.ProcessingCommands,
                        ShokoService.CmdProcessorImages.Paused
                    },
                });
        }
    }
}
