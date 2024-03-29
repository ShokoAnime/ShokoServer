using System;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using Quartz.Simpl;
using Quartz.Spi;
using Shoko.Server.Scheduling.GenericJobBuilder;
using Shoko.Server.Scheduling.Jobs;
using Shoko.Server.Server;

namespace Shoko.Server.Scheduling;

public class JobFactory : MicrosoftDependencyInjectionJobFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<JobFactory> _logger;

    // Used by Quartz
    public override IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
    {
        try
        {
            // NewJob calls InstantiateJob, then applies the JobDataMap
            var job =  base.NewJob(bundle, scheduler);
            // After, we check for BaseJob and apply PostInit
            if (job is BaseJob baseJob)
            {
                // This is used to make a logger with a useful name
                baseJob._logger = _loggerFactory.CreateLogger(bundle.JobDetail.Key.Name.Replace(".", "․"));
                if (ServerState.Instance.DatabaseAvailable) baseJob.PostInit();
                else baseJob._logger.LogTrace("Server not started yet. Details will be less descriptive");
            }
            else if (job.GetType().Name.Equals("ScopedJob"))
            {
                var innerJobProperty = job.GetType().GetProperty("InnerJob", BindingFlags.Instance | BindingFlags.NonPublic);
                if (innerJobProperty == null) return job;
                var innerJob = innerJobProperty.GetValue(job) as IJob;
                if (innerJob is not BaseJob innerBaseJob) return job;
                innerBaseJob._logger = _loggerFactory.CreateLogger(bundle.JobDetail.Key.Name.Replace(".", "․"));
                if (ServerState.Instance.DatabaseAvailable) innerBaseJob.PostInit();
                else innerBaseJob._logger.LogTrace("Server not started yet. Details will be less descriptive");
            }
            return job;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "There was an error initializing Job: {Key}", bundle.JobDetail.Key);
            throw;
        }
    }

    // The rest used by us
    public T CreateJob<T>(Action<T> ctor = null) where T : BaseJob
    {
        try
        {
            var job = _serviceProvider.GetRequiredService<T>();
            ctor?.Invoke(job);
            var jobKey = ctor == null ? JobKeyBuilder<T>.Create().Build() : JobKeyBuilder<T>.Create().UsingJobData(ctor).Build();
            job._logger = _loggerFactory.CreateLogger(jobKey.Name);
            job.PostInit();
            return job;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "There was an error initializing Generic Job: {Type}", typeof(T).Name);
            throw;
        }
    }

    public BaseJob CreateJob(IJobDetail jobDetails)
    {
        try
        {
            var type = jobDetails.JobType;
            if (_serviceProvider.GetService(type) is not BaseJob job) return null;
            SetObjectProperties(job, jobDetails.JobDataMap);
            job._logger = _loggerFactory.CreateLogger(jobDetails.Key.Name);
            job.PostInit();
            return job;
        }
        catch
        {
            return null;
        }
    }

    public JobFactory(IServiceProvider serviceProvider, IOptions<QuartzOptions> options, ILoggerFactory loggerFactory, ILogger<JobFactory> logger) : base(serviceProvider, options)
    {
        _serviceProvider = serviceProvider;
        _loggerFactory = loggerFactory;
        _logger = logger;
    }
}
