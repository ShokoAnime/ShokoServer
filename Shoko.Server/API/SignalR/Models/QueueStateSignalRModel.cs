using Shoko.Models.Queue;

namespace Shoko.Server.API.SignalR.Models
{
    public class QueueStateSignalRModel
    {
        public QueueStateEnum State { get; set; }
        public string Description { get; set; }
    }
}