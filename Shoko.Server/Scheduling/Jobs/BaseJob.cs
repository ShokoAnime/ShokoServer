using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Serialization;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Quartz;

namespace Shoko.Server.Scheduling.Jobs;

[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public abstract class BaseJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            await Process();
        }
        catch (Exception ex)
        {
            // _logger.LogError(ex, "Job threw an error on Execution: {Job} | Error -> {Ex}", context.JobDetail.Key, ex);
            throw new JobExecutionException(msg: ex.Message, cause: ex);
        }
    }

    public abstract Task Process();
 
    [XmlIgnore] [JsonIgnore] public ILogger _logger;
    [XmlIgnore] [JsonIgnore] public abstract string TypeName { get; }
    [XmlIgnore] [JsonIgnore] public abstract string Title { get; }
    [XmlIgnore] [JsonIgnore] public virtual Dictionary<string, object> Details { get; } = new();

    public virtual void PostInit() { }
}

public abstract class BaseJob<T> : BaseJob
{
    public override abstract Task<T> Process();
}
