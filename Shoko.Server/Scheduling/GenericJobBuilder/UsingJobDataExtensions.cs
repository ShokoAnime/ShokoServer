using System;
using Quartz;

namespace Shoko.Server.Scheduling.GenericJobBuilder;

public static class UsingJobDataExtensions
{
    /// <summary>
    /// Bind the given <see cref="IJob"/> model to the <see cref="JobDataMap" />.
    /// </summary>
    ///<returns>the updated JobBuilder</returns>
    /// <seealso cref="IJobDetail.JobDataMap" />
    public static IJobConfiguratorWithData<T> UsingJobData<T>(this IJobConfigurator<T> jobConfigurator, Action<T> ctor) where T : class, IJob
    {
        var map = JobDataMapBuilder.FromType(ctor);
        jobConfigurator.GetJobData().PutAll(map);
        return (IJobConfiguratorWithData<T>)jobConfigurator;
    }

    /// <summary>
    /// Bind the given <see cref="IJob"/> model to the <see cref="JobDataMap" />.
    /// </summary>
    ///<returns>the updated JobBuilder</returns>
    /// <seealso cref="IJobDetail.JobDataMap" />
    public static IJobConfiguratorWithDataAndIdentity<T> UsingJobData<T>(this IJobConfiguratorWithIdentity<T> jobConfigurator, Action<T> ctor) where T : class, IJob
    {
        var map = JobDataMapBuilder.FromType(ctor);
        jobConfigurator.GetJobData().PutAll(map);
        return (IJobConfiguratorWithDataAndIdentity<T>)jobConfigurator;
    }

    /// <summary>
    /// Bind the given <see cref="IJob"/> model to the <see cref="JobDataMap" />.
    /// </summary>
    ///<returns>the updated JobBuilder</returns>
    /// <seealso cref="IJobDetail.JobDataMap" />
    [Obsolete("WithGeneratedIdentity was used before UsingJobData. This will cause the JobKey to be missing data")]
    public static IJobConfiguratorWithDataAndIdentity<T> UsingJobData<T>(this IJobConfiguratorWithGeneratedIdentity<T> jobConfigurator, Action<T> ctor) where T : class, IJob
    {
        var map = JobDataMapBuilder.FromType(ctor);
        jobConfigurator.GetJobData().PutAll(map);
        return (IJobConfiguratorWithDataAndIdentity<T>)jobConfigurator;
    }
}
