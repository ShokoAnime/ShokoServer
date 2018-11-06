using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Shoko.Commons.Queue;

namespace Shoko.Server.CommandQueue.Commands
{
    public interface ICommand 
    {
        Task<CommandResult> RunAsync(IProgress<ICommandProgress> progress=null,CancellationToken token=default(CancellationToken));
        string Serialize();
        [JsonIgnore]
        string ParallelTag { get; set; }
        [JsonIgnore]
        int ParallelMax { get; set; }
        [JsonIgnore]
        int Priority { get; set; }
        [JsonIgnore]
        string Id { get; }
        [JsonIgnore]
        int Retries { get; set; }
        [JsonIgnore]
        string Batch { get; set; }
        [JsonIgnore]
        QueueStateStruct PrettyDescription { get; }
        [JsonIgnore]
        WorkTypes WorkType { get; }
    }


}