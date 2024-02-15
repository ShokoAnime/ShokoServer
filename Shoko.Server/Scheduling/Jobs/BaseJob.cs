using System;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Quartz;
using Shoko.Commons.Queue;

namespace Shoko.Server.Scheduling.Jobs;

public abstract class BaseJob : IShokoJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            await Process();
        }
        catch (Exception ex)
        {
            throw new JobExecutionException(msg: ex.Message, refireImmediately: false, cause: ex);
        }
    }

    public abstract Task Process();
 
    [XmlIgnore][JsonIgnore] public ILogger _logger;
    [XmlIgnore][JsonIgnore] public abstract QueueStateStruct Description { get; }
    [XmlIgnore][JsonIgnore] public abstract string Name { get; }

    public virtual void PostInit() { }
}

public abstract class BaseJob<T> : BaseJob
{
    public override abstract Task<T> Process();
}
