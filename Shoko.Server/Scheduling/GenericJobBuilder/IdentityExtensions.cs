using Quartz;

#nullable enable
namespace Shoko.Server.Scheduling.GenericJobBuilder;

public static class IdentityExtensions
{
    /// <summary>
    /// Generate a <see cref="JobKey" /> to identify the JobDetail from the set JobDataMap using <see cref="JobKey"/> on members.
    /// If none are marked, then all public properties will be considered, in the default order, with the member names.
    /// Only non-default values will be added to the <see cref="Server"/>
    /// </summary>
    /// <remarks>
    /// <para>If none of the 'withIdentity' methods are set on the JobBuilder,
    /// then a random, unique JobKey will be generated.</para>
    /// </remarks>
    /// <returns>the updated JobBuilder</returns>
    /// <seealso cref="Shoko" />
    public static IJobConfiguratorWithDataAndIdentity<T> WithGeneratedIdentity<T>(this IJobConfiguratorWithData<T> jobConfigurator, string? group = null) where T : class, IJob
    {
        var key = JobKeyBuilder<T>.Create().WithGroup(group).UsingJobData(jobConfigurator.GetJobData()).Build();
        return jobConfigurator.WithIdentity(key);
    }

    /// <summary>
    /// Generate a <see cref="JobKey" /> to identify the JobDetail from the set JobDataMap using <see cref="JobKey"/> on members.
    /// If none are marked, then all public properties will be considered, in the default order, with the member names.
    /// Only non-default values will be added to the <see cref="Server"/>
    /// </summary>
    /// <remarks>
    /// <para>If none of the 'withIdentity' methods are set on the JobBuilder,
    /// then a random, unique JobKey will be generated.</para>
    /// </remarks>
    /// <returns>the updated JobBuilder</returns>
    /// <seealso cref="Shoko" />
    public static IJobConfiguratorWithGeneratedIdentity<T> WithGeneratedIdentity<T>(this IJobConfigurator<T> jobConfigurator, string? group = null) where T : class, IJob
    {
        var key = JobKeyBuilder<T>.Create().WithGroup(group).Build();
        return (IJobConfiguratorWithGeneratedIdentity<T>)jobConfigurator.WithIdentity(key);
    }

    /// <summary>
    /// Use a <see cref="JobKey" /> with the given name and default group to
    /// identify the JobDetail.
    /// </summary>
    /// <remarks>
    /// <para>If none of the 'withIdentity' methods are set on the JobBuilder,
    /// then a random, unique JobKey will be generated.</para>
    /// </remarks>
    /// <param name="jobConfigurator"></param>
    /// <param name="name">the name element for the Job's JobKey</param>
    /// <returns>the updated JobBuilder</returns>
    /// <seealso cref="JobKey" />
    /// <seealso cref="IJobDetail.Key" />
    public static IJobConfiguratorWithIdentity<T> WithIdentity<T>(this IJobConfigurator<T> jobConfigurator, string name)
        where T : class, IJob
    {
        jobConfigurator.SetJobKey(new JobKey(name));
        return (IJobConfiguratorWithIdentity<T>)jobConfigurator;
    }

    /// <summary>
    /// Use a <see cref="JobKey" /> with the given name and group to
    /// identify the JobDetail.
    /// </summary>
    /// <remarks>
    /// <para>If none of the 'withIdentity' methods are set on the JobBuilder,
    /// then a random, unique JobKey will be generated.</para>
    /// </remarks>
    /// <param name="jobConfigurator"></param>
    /// <param name="name">the name element for the Job's JobKey</param>
    /// <param name="group"> the group element for the Job's JobKey</param>
    /// <returns>the updated JobBuilder</returns>
    /// <seealso cref="JobKey" />
    /// <seealso cref="IJobDetail.Key" />
    public static IJobConfiguratorWithIdentity<T> WithIdentity<T>(this IJobConfigurator<T> jobConfigurator, string name, string group)
        where T : class, IJob
    {
        jobConfigurator.SetJobKey(new JobKey(name, group));
        return (IJobConfiguratorWithIdentity<T>)jobConfigurator;
    }

    /// <summary>
    /// Use a <see cref="JobKey" /> to identify the JobDetail.
    /// </summary>
    /// <remarks>
    /// <para>If none of the 'withIdentity' methods are set on the JobBuilder,
    /// then a random, unique JobKey will be generated.</para>
    /// </remarks>
    /// <param name="jobConfigurator"></param>
    /// <param name="key">the Job's JobKey</param>
    /// <returns>the updated JobBuilder</returns>
    /// <seealso cref="JobKey" />
    /// <seealso cref="IJobDetail.Key" />
    public static IJobConfiguratorWithIdentity<T> WithIdentity<T>(this IJobConfigurator<T> jobConfigurator, JobKey key)
        where T : class, IJob
    {
        jobConfigurator.SetJobKey(key);
        return (IJobConfiguratorWithIdentity<T>)jobConfigurator;
    }

    /// <summary>
    /// Use a <see cref="JobKey" /> with the given name and default group to
    /// identify the JobDetail.
    /// </summary>
    /// <remarks>
    /// <para>If none of the 'withIdentity' methods are set on the JobBuilder,
    /// then a random, unique JobKey will be generated.</para>
    /// </remarks>
    /// <param name="jobConfigurator"></param>
    /// <param name="name">the name element for the Job's JobKey</param>
    /// <returns>the updated JobBuilder</returns>
    /// <seealso cref="JobKey" />
    /// <seealso cref="IJobDetail.Key" />
    public static IJobConfiguratorWithDataAndIdentity<T> WithIdentity<T>(this IJobConfiguratorWithData<T> jobConfigurator, string name)
        where T : class, IJob
    {
        jobConfigurator.SetJobKey(new JobKey(name));
        return (IJobConfiguratorWithDataAndIdentity<T>)jobConfigurator;
    }

    /// <summary>
    /// Use a <see cref="JobKey" /> with the given name and group to
    /// identify the JobDetail.
    /// </summary>
    /// <remarks>
    /// <para>If none of the 'withIdentity' methods are set on the JobBuilder,
    /// then a random, unique JobKey will be generated.</para>
    /// </remarks>
    /// <param name="jobConfigurator"></param>
    /// <param name="name">the name element for the Job's JobKey</param>
    /// <param name="group"> the group element for the Job's JobKey</param>
    /// <returns>the updated JobBuilder</returns>
    /// <seealso cref="JobKey" />
    /// <seealso cref="IJobDetail.Key" />
    public static IJobConfiguratorWithDataAndIdentity<T> WithIdentity<T>(this IJobConfiguratorWithData<T> jobConfigurator, string name, string group)
        where T : class, IJob
    {
        jobConfigurator.SetJobKey(new JobKey(name, group));
        return (IJobConfiguratorWithDataAndIdentity<T>)jobConfigurator;
    }

    /// <summary>
    /// Use a <see cref="JobKey" /> to identify the JobDetail.
    /// </summary>
    /// <remarks>
    /// <para>If none of the 'withIdentity' methods are set on the JobBuilder,
    /// then a random, unique JobKey will be generated.</para>
    /// </remarks>
    /// <param name="jobConfigurator"></param>
    /// <param name="key">the Job's JobKey</param>
    /// <returns>the updated JobBuilder</returns>
    /// <seealso cref="JobKey" />
    /// <seealso cref="IJobDetail.Key" />
    public static IJobConfiguratorWithDataAndIdentity<T> WithIdentity<T>(this IJobConfiguratorWithData<T> jobConfigurator, JobKey key)
        where T : class, IJob
    {
        jobConfigurator.SetJobKey(key);
        return (IJobConfiguratorWithDataAndIdentity<T>)jobConfigurator;
    }

    /// <summary>
    /// Use a random Guid for an identity. This is the default if no Identity was set
    /// </summary>
    /// <remarks>
    /// <para>If none of the 'withIdentity' methods are set on the JobBuilder,
    /// then a random, unique JobKey will be generated.</para>
    /// </remarks>
    public static IJobConfiguratorWithIdentity<T> WithDefaultIdentity<T>(this IJobConfigurator<T> jobConfigurator)
        where T : class, IJob
    {
        return (IJobConfiguratorWithIdentity<T>)jobConfigurator;
    }

    /// <summary>
    /// Use a random Guid for an identity. This is the default if no Identity was set
    /// </summary>
    /// <remarks>
    /// <para>If none of the 'withIdentity' methods are set on the JobBuilder,
    /// then a random, unique JobKey will be generated.</para>
    /// </remarks>
    public static IJobConfiguratorWithDataAndIdentity<T> WithDefaultIdentity<T>(this IJobConfiguratorWithData<T> jobConfigurator)
        where T : class, IJob
    {
        return (IJobConfiguratorWithDataAndIdentity<T>)jobConfigurator;
    }
}
