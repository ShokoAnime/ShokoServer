using Microsoft.AspNetCore.SignalR;
using Shoko.Server.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shoko.Server.API.SignalR
{
    public class EventsHub : Hub
    {
        public EventsHub()
        {
            ShokoService.CmdProcessorGeneral.OnQueueCountChangedEvent += (e) => OnQueueCountChangedEvent("general", e);
            ShokoService.CmdProcessorHasher.OnQueueCountChangedEvent += (e) => OnQueueCountChangedEvent("hasher", e);
            ShokoService.CmdProcessorImages.OnQueueCountChangedEvent += (e) => OnQueueCountChangedEvent("images", e);

            ShokoService.CmdProcessorGeneral.OnQueueStateChangedEvent += (e) => OnQueueStateChangedEvent("general", e);
            ShokoService.CmdProcessorHasher.OnQueueStateChangedEvent += (e) => OnQueueStateChangedEvent("hasher", e);
            ShokoService.CmdProcessorImages.OnQueueStateChangedEvent += (e) => OnQueueStateChangedEvent("images", e);
        }

        private async void OnQueueStateChangedEvent(string queue, QueueStateEventArgs e)
        {
            await Clients.All.SendAsync("QueueStateChanged", queue, e.QueueState.formatMessage());
        }

        private async void OnQueueCountChangedEvent(string queue, Commands.QueueCountEventArgs ev)
        {
            await Clients.All.SendAsync("QueueCountChanged", queue, ev.QueueCount);
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
            await Clients.Caller.SendAsync("CommandProcessingStatus", new {
                General = new
                {
                    State = ShokoService.CmdProcessorGeneral.QueueState.formatMessage(),
                    Count = ShokoService.CmdProcessorGeneral.QueueCount,
                    ProcessingCommands = ShokoService.CmdProcessorGeneral.ProcessingCommands,
                    Paused = ShokoService.CmdProcessorGeneral.Paused
                },

                Hasher = new
                {
                    State = ShokoService.CmdProcessorHasher.QueueState.formatMessage(),
                    Count = ShokoService.CmdProcessorHasher.QueueCount,
                    ProcessingCommands = ShokoService.CmdProcessorHasher.ProcessingCommands,
                    Paused = ShokoService.CmdProcessorHasher.Paused
                },

                Images = new
                {
                    State = ShokoService.CmdProcessorImages.QueueState.formatMessage(),
                    Count = ShokoService.CmdProcessorImages.QueueCount,
                    ProcessingCommands = ShokoService.CmdProcessorImages.ProcessingCommands,
                    Paused = ShokoService.CmdProcessorImages.Paused
                },
            });
        }
    }
}
