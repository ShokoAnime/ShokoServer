using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Quartz;
using Quartz.Impl.Matchers;
using Quartz.Spi;

namespace Shoko.Server.Scheduling;

public partial class ThreadPooledJobStore : IJobStore
{
    /// <summary>
    /// Retrieve the <see cref="IJobDetail" /> for the given
    /// <see cref="IJob" />.
    /// </summary>
    /// <param name="jobKey">The key identifying the job.</param>
    /// <param name="cancellationToken">The cancellation instruction.</param>
    /// <returns>The desired <see cref="IJob" />, or null if there is no match.</returns>
    Task<IJobDetail> IJobStore.RetrieveJob(JobKey jobKey, CancellationToken cancellationToken)
    {
        return ExecuteInNonManagedTXLock(LockTriggerAccess, conn => RetrieveJob(conn, jobKey, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Retrieve the given <see cref="ITrigger" />.
    /// </summary>
    /// <param name="triggerKey">The key identifying the trigger.</param>
    /// <param name="cancellationToken">The cancellation instruction.</param>
    /// <returns>The desired <see cref="ITrigger" />, or null if there is no match.</returns>
    Task<IOperableTrigger> IJobStore.RetrieveTrigger(TriggerKey triggerKey, CancellationToken cancellationToken)
    {
        return ExecuteInNonManagedTXLock(LockTriggerAccess, conn => RetrieveTrigger(conn, triggerKey, cancellationToken), cancellationToken);
    }

    
    /// <summary>
    /// Get the current state of the identified <see cref="ITrigger" />.
    /// </summary>
    /// <seealso cref="TriggerState.Normal" />
    /// <seealso cref="TriggerState.Paused" />
    /// <seealso cref="TriggerState.Complete" />
    /// <seealso cref="TriggerState.Error" />
    /// <seealso cref="TriggerState.None" />
    Task<TriggerState> IJobStore.GetTriggerState(TriggerKey triggerKey, CancellationToken cancellationToken)
    {
        return ExecuteInNonManagedTXLock(LockTriggerAccess, conn => GetTriggerState(conn, triggerKey, cancellationToken), cancellationToken);
    }

    
    /// <summary>
    /// Retrieve the given <see cref="ITrigger" />.
    /// </summary>
    /// <param name="calName">The name of the <see cref="ICalendar" /> to be retrieved.</param>
    /// <param name="cancellationToken">The cancellation instruction.</param>
    /// <returns>The desired <see cref="ICalendar" />, or null if there is no match.</returns>
    Task<ICalendar> IJobStore.RetrieveCalendar(string calName, CancellationToken cancellationToken)
    {
        return ExecuteInNonManagedTXLock(LockTriggerAccess, conn => RetrieveCalendar(conn, calName, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Get the number of <see cref="IJob" /> s that are
    /// stored in the <see cref="IJobStore" />.
    /// </summary>
    Task<int> IJobStore.GetNumberOfJobs(CancellationToken cancellationToken)
    {
        return ExecuteInNonManagedTXLock(LockTriggerAccess, conn => GetNumberOfJobs(conn, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Get the number of <see cref="ITrigger" /> s that are
    /// stored in the <see cref="IJobStore" />.
    /// </summary>
    Task<int> IJobStore.GetNumberOfTriggers(CancellationToken cancellationToken)
    {
        return ExecuteInNonManagedTXLock(LockTriggerAccess, conn => GetNumberOfTriggers(conn, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Get the number of <see cref="ICalendar" /> s that are
    /// stored in the <see cref="IJobStore" />.
    /// </summary>
    Task<int> IJobStore.GetNumberOfCalendars(CancellationToken cancellationToken)
    {
        return ExecuteInNonManagedTXLock(LockTriggerAccess, conn => GetNumberOfCalendars(conn, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Get the names of all of the <see cref="IJob" /> s that
    /// have the given group name.
    /// </summary>
    /// <remarks>
    /// If there are no jobs in the given group name, the result should be a
    /// zero-length array (not <see langword="null" />).
    /// </remarks>
    Task<IReadOnlyCollection<JobKey>> IJobStore.GetJobKeys(GroupMatcher<JobKey> matcher, CancellationToken cancellationToken)
    {
        return ExecuteInNonManagedTXLock(LockTriggerAccess, conn => GetJobNames(conn, matcher, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Determine whether a <see cref="ICalendar" /> with the given identifier already
    /// exists within the scheduler.
    /// </summary>
    /// <remarks>
    /// </remarks>
    /// <param name="calName">the identifier to check for</param>
    /// <param name="cancellationToken">The cancellation instruction.</param>
    /// <returns>true if a calendar exists with the given identifier</returns>
    Task<bool> IJobStore.CalendarExists(string calName, CancellationToken cancellationToken)
    {
        return ExecuteInNonManagedTXLock(LockTriggerAccess, conn => CheckExists(conn, calName, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Determine whether a <see cref="IJob"/> with the given identifier already
    /// exists within the scheduler.
    /// </summary>
    /// <remarks>
    /// </remarks>
    /// <param name="jobKey">the identifier to check for</param>
    /// <param name="cancellationToken">The cancellation instruction.</param>
    /// <returns>true if a Job exists with the given identifier</returns>
    Task<bool> IJobStore.CheckExists(JobKey jobKey, CancellationToken cancellationToken)
    {
        return ExecuteInNonManagedTXLock(LockTriggerAccess, conn => CheckExists(conn, jobKey, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Determine whether a <see cref="ITrigger" /> with the given identifier already
    /// exists within the scheduler.
    /// </summary>
    /// <remarks>
    /// </remarks>
    /// <param name="triggerKey">the identifier to check for</param>
    /// <param name="cancellationToken">The cancellation instruction.</param>
    /// <returns>true if a Trigger exists with the given identifier</returns>
    Task<bool> IJobStore.CheckExists(TriggerKey triggerKey, CancellationToken cancellationToken)
    {
        return ExecuteInNonManagedTXLock(LockTriggerAccess, conn => CheckExists(conn, triggerKey, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Get the names of all of the <see cref="ITrigger" /> s
    /// that have the given group name.
    /// </summary>
    /// <remarks>
    /// If there are no triggers in the given group name, the result should be a
    /// zero-length array (not <see langword="null" />).
    /// </remarks>
    Task<IReadOnlyCollection<TriggerKey>> IJobStore.GetTriggerKeys(GroupMatcher<TriggerKey> matcher, CancellationToken cancellationToken)
    {
        return ExecuteInNonManagedTXLock(LockTriggerAccess, conn => GetTriggerNames(conn, matcher, cancellationToken), cancellationToken);
    }
    
    /// <summary>
    /// Get the names of all of the <see cref="IJob" />
    /// groups.
    /// </summary>
    ///
    /// <remarks>
    /// If there are no known group names, the result should be a zero-length
    /// array (not <see langword="null" />).
    /// </remarks>
    Task<IReadOnlyCollection<string>> IJobStore.GetJobGroupNames(CancellationToken cancellationToken)
    {
        return ExecuteInNonManagedTXLock(LockTriggerAccess, conn => GetJobGroupNames(conn, cancellationToken), cancellationToken);
    }
    
    /// <summary>
    /// Get the names of all of the <see cref="ITrigger" />
    /// groups.
    /// </summary>
    ///
    /// <remarks>
    /// If there are no known group names, the result should be a zero-length
    /// array (not <see langword="null" />).
    /// </remarks>
    Task<IReadOnlyCollection<string>> IJobStore.GetTriggerGroupNames(CancellationToken cancellationToken)
    {
        return ExecuteInNonManagedTXLock(LockTriggerAccess, conn => GetTriggerGroupNames(conn, cancellationToken), cancellationToken);
    }
    
    /// <summary>
    /// Get the names of all of the <see cref="ICalendar" /> s
    /// in the <see cref="IJobStore" />.
    /// </summary>
    /// <remarks>
    /// If there are no Calendars in the given group name, the result should be
    /// a zero-length array (not <see langword="null" />).
    /// </remarks>
    Task<IReadOnlyCollection<string>> IJobStore.GetCalendarNames(CancellationToken cancellationToken)
    {
        return ExecuteInNonManagedTXLock(LockTriggerAccess, conn => GetCalendarNames(conn, cancellationToken), cancellationToken);
    }
    
    /// <summary>
    /// Get all of the Triggers that are associated to the given Job.
    /// </summary>
    /// <remarks>
    /// If there are no matches, a zero-length array should be returned.
    /// </remarks>
    Task<IReadOnlyCollection<IOperableTrigger>> IJobStore.GetTriggersForJob(JobKey jobKey, CancellationToken cancellationToken)
    {
        return ExecuteInNonManagedTXLock(LockTriggerAccess, conn => GetTriggersForJob(conn, jobKey, cancellationToken), cancellationToken);
    }

    Task<IReadOnlyCollection<string>> IJobStore.GetPausedTriggerGroups(CancellationToken cancellationToken)
    {
        return ExecuteInNonManagedTXLock(LockTriggerAccess, conn => GetPausedTriggerGroups(conn, cancellationToken), cancellationToken);
    }
}
