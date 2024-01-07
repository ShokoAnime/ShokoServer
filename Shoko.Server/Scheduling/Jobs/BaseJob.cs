using System.Threading.Tasks;
using System.Xml.Serialization;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Quartz;
using Shoko.Commons.Queue;

namespace Shoko.Server.Scheduling.Jobs;

/// <summary>
/// Not sure what might go on here, but it prevents making one later
/// </summary>
public abstract class BaseJob : IJob
{
    public abstract Task Execute(IJobExecutionContext context);
 
    [XmlIgnore][JsonIgnore] protected readonly ILogger Logger;
    [XmlIgnore][JsonIgnore] public abstract QueueStateStruct Description { get; }
    
    protected BaseJob(ILoggerFactory loggerFactory)
    {
        Logger = loggerFactory.CreateLogger(GetType());
    }

    protected BaseJob() { }
}
