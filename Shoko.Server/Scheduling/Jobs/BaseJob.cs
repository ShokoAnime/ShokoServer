using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Serialization;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Quartz;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;

namespace Shoko.Server.Scheduling.Jobs;

[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public abstract class BaseJob : IJob
{
    [XmlIgnore, JsonIgnore]
    public ILogger _logger;

    [XmlIgnore, JsonIgnore]
    public abstract string TypeName { get; }

    [XmlIgnore, JsonIgnore]
    public abstract string Title { get; }

    [XmlIgnore, JsonIgnore]
    public virtual Dictionary<string, object> Details { get; } = [];

    public async ValueTask Execute(IJobExecutionContext context)
    {
        try
        {
            await Process();
        }
        catch (NotLoggedInException ex)
        {
            // INVALID SESSION or UNKNOWN COMMAND returned, re-fire job immediately to try again with re-login
            throw new JobExecutionException(ex.Message, ex, refireImmediately: true);
        }
        catch (Exception ex)
        {
            // TODO: Reschedule job on AniDBBannedException
            // _logger.LogError(ex, "Job threw an error on Execution: {Job} | Error -> {Ex}", context.JobDetail.Key, ex);
            throw new JobExecutionException(ex.Message, ex);
        }
    }

    public abstract Task Process();

    public virtual void PostInit() { }
}

public abstract class BaseJob<T> : BaseJob
{
    public override abstract Task<T> Process();
}
