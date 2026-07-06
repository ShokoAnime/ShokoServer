using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Serialization;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Chain;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;

#pragma warning disable CS8618
namespace Shoko.Server.Scheduling.Jobs;

[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public abstract class BaseJob : IQueueJob
{
    [XmlIgnore, JsonIgnore]
    protected ILogger _logger { get; private set; }

    /// <inheritdoc/>
    public virtual void Setup(IServiceProvider serviceProvider)
    {
        _logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());
    }

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
        if (_logger == null) throw new InvalidOperationException("Job not properly setup");
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
    private IJobChainContextAccessor? _chainContextAccessor;

    public override void Setup(IServiceProvider serviceProvider)
    {
        base.Setup(serviceProvider);
        _chainContextAccessor = serviceProvider.GetService<IJobChainContextAccessor>();
    }

    public sealed override async Task Execute()
    {
        var result = await Process();
        _chainContextAccessor?.SetResult(result);
    }

    public new abstract Task<T> Process();
}
