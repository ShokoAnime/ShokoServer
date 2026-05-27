using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Serialization;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;

namespace Shoko.Server.Scheduling.Jobs;

[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public abstract class BaseJob : IQueueJob
{
    [XmlIgnore, JsonIgnore]
    public ILogger _logger;

    [XmlIgnore, JsonIgnore]
    public abstract string TypeName { get; }

    [XmlIgnore, JsonIgnore]
    public abstract string Title { get; }

    [XmlIgnore, JsonIgnore]
    public virtual Dictionary<string, object> Details { get; } = [];

    /// <summary>
    /// Called by the worker. Catches AniDB transient exceptions and converts them to
    /// <see cref="RequeueJobException"/> so the job re-queues without incrementing its retry count.
    /// </summary>
    public async Task Process()
    {
        try
        {
            await Execute();
        }
        catch (NotLoggedInException)
        {
            throw new RequeueJobException();
        }
        catch (LoginFailedException)
        {
            throw new RequeueJobException();
        }
        catch (AniDBBannedException)
        {
            throw new RequeueJobException();
        }
    }

    /// <summary>Implement job logic here. Exceptions propagate to the retry policy.</summary>
    public abstract Task Execute();

    public virtual void PostInit() { }
}

public abstract class BaseJob<T> : BaseJob
{
    public sealed override async Task Execute() => await Process();
    public new abstract Task<T> Process();
}
