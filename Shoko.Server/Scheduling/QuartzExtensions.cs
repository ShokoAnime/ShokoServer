using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Quartz;
using QuartzJobFactory;

namespace Shoko.Server.Scheduling;

public static class QuartzExtensions
{
    /// <summary>
    /// Queue a job of type T with the data map setter and generated identity
    /// </summary>
    /// <param name="scheduler"></param>
    /// <param name="data">Job Data Constructor</param>
    /// <typeparam name="T">Job Type</typeparam>
    /// <returns></returns>
    public static async Task<DateTimeOffset> StartJob<T>(this IScheduler scheduler, Action<T> data = null) where T : class, IJob
    {
        if (data == null)
            return await scheduler.StartJob(JobBuilder<T>.Create().WithGeneratedIdentity().Build());
        return await scheduler.StartJob(JobBuilder<T>.Create().UsingJobData(data).WithGeneratedIdentity().Build());
    }
    
    /// <summary>
    /// Force a job of type T with the data map setter and generated identity to run asap
    /// </summary>
    /// <param name="scheduler"></param>
    /// <param name="data">Job Data Constructor</param>
    /// <typeparam name="T">Job Type</typeparam>
    /// <returns></returns>
    public static async Task<DateTimeOffset> StartJobNow<T>(this IScheduler scheduler, Action<T> data = null) where T : class, IJob
    {
        if (data == null)
            return await scheduler.StartJob(JobBuilder<T>.Create().WithGeneratedIdentity().Build(), priority:1);
        return await scheduler.StartJob(JobBuilder<T>.Create().UsingJobData(data).WithGeneratedIdentity().Build(), priority:1);
    }
    
    /// <summary>
    /// This will add an array of parameters to a SqlCommand. This is used for an IN statement.
    /// Use the returned value for the IN part of your SQL call. (i.e. SELECT * FROM table WHERE field IN ({paramNameRoot}))
    /// </summary>
    /// <param name="cmd">The SqlCommand object to add parameters to.</param>
    /// <param name="paramNameRoot">What the parameter should be named followed by a unique value for each value. This value surrounded by {} in the CommandText will be replaced.</param>
    /// <param name="values">The array of strings that need to be added as parameters.</param>
    /// <param name="dbType">One of the System.Data.SqlDbType values. If null, determines type based on T.</param>
    /// <param name="size">The maximum size, in bytes, of the data within the column. The default value is inferred from the parameter value.</param>
    public static void AddArrayParameters<T>(this IDbCommand cmd, string paramNameRoot, IEnumerable<T> values, DbType? dbType = null, int? size = null)
    {
        /* An array cannot be simply added as a parameter to a SqlCommand so we need to loop through things and add it manually. 
         * Each item in the array will end up being it's own SqlParameter so the return value for this must be used as part of the
         * IN statement in the CommandText.
         */
        var parameterNames = new List<string>();
        var paramNbr = 1;
        foreach (var value in values)
        {
            var paramName = $"@{paramNameRoot}{paramNbr++}";
            parameterNames.Add(paramName);
            var p = cmd.CreateParameter();
            p.ParameterName = paramName;
            p.Value = value;
            if (dbType.HasValue)
                p.DbType = dbType.Value;
            if (size.HasValue)
                p.Size = size.Value;
            cmd.Parameters.Add(p);
        }

        cmd.CommandText = cmd.CommandText.Replace("@" + paramNameRoot, string.Join(",", parameterNames));
    }
}
