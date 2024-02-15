using System;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using Quartz.Simpl;
using Quartz.Spi;
using QuartzJobFactory;
using Shoko.Server.Scheduling.Jobs;

namespace Shoko.Server.Scheduling;

public class JobFactory : MicrosoftDependencyInjectionJobFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILoggerFactory _loggerFactory;

    public override IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
    {
        // NewJob calls InstantiateJob, then applies the JobDataMap
        var job =  base.NewJob(bundle, scheduler);
        // After, we check for BaseJob and apply PostInit
        if (job is BaseJob baseJob)
        {
            // This is used to make a logger with a useful name
            baseJob._logger = _loggerFactory.CreateLogger(bundle.JobDetail.Key.Name.Replace(".", "․"));
            baseJob.PostInit();
        }
        else if (job.GetType().Name.Equals("ScopedJob"))
        {
            var innerJobProperty = job.GetType().GetProperty("InnerJob", BindingFlags.Instance | BindingFlags.NonPublic);
            if (innerJobProperty == null) return job;
            var innerJob = innerJobProperty.GetValue(job) as IJob;
            if (innerJob is not BaseJob innerBaseJob) return job;
            innerBaseJob._logger = _loggerFactory.CreateLogger(bundle.JobDetail.Key.Name.Replace(".", "․"));
            innerBaseJob.PostInit();
        }
        return job;
    }

    public T CreateJob<T>(Action<T> ctor = null) where T : BaseJob
    {
        var job = _serviceProvider.GetRequiredService<T>();
        ctor?.Invoke(job);
        var jobKey = ctor == null ? JobKeyBuilder<T>.Create().Build() : JobKeyBuilder<T>.Create().UsingJobData(ctor).Build();
        job._logger = _loggerFactory.CreateLogger(jobKey.Name);
        job.PostInit();
        return job;
    }

    public T CreateJob<T>(IJobDetail jobDetails) where T : BaseJob
    {
        return CreateJob<T>(jobDetails.JobDataMap);
    }

    public T CreateJob<T>(JobDataMap dataMap) where T : BaseJob
    {
        var job = _serviceProvider.GetRequiredService<T>();
        SetObjectProperties(job, dataMap);
        var jobKey = JobKeyBuilder<T>.Create().UsingJobData(dataMap).Build();
        job._logger = _loggerFactory.CreateLogger(jobKey.Name);
        job.PostInit();
        return job;
    }

    public BaseJob CreateJob(IJobDetail jobDetails)
    {
        var type = jobDetails.JobType;
        if (_serviceProvider.GetService(type) is not BaseJob job) return null;
        SetObjectProperties(job, jobDetails.JobDataMap);
        job._logger = _loggerFactory.CreateLogger(jobDetails.Key.Name);
        job.PostInit();
        return job;
    }

    public JobFactory(IServiceProvider serviceProvider, IOptions<QuartzOptions> options, ILoggerFactory loggerFactory) : base(serviceProvider, options)
    {
        _serviceProvider = serviceProvider;
        _loggerFactory = loggerFactory;
    }
}
