using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Shoko.Commons.Queue;

namespace Shoko.Server.CommandQueue.Commands
{
    public interface ICommand 
    {
        Task RunAsync(IProgress<ICommand> progress=null,CancellationToken token=default(CancellationToken));
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
        int MaxRetries { get; set; }
        [JsonIgnore]
        string Batch { get; set; }
        [JsonIgnore]
        QueueStateStruct PrettyDescription { get; }
        [JsonIgnore]
        string WorkType { get; }
        [JsonIgnore]
        double Progress { get;  }
        [JsonIgnore]
        CommandStatus Status { get;}
        [JsonIgnore]
        string Error { get; }
        [JsonIgnore]
        int RetryFutureInSeconds { get; set; }
        [JsonIgnore]
        List<Type> GenericPreconditions { get; set; }

    }
}