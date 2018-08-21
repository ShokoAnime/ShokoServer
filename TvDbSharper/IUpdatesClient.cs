namespace TvDbSharper
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using TvDbSharper.Dto;

    /// <summary>
    /// Used for getting series that have been recently updated
    /// </summary>
    public interface IUpdatesClient
    {
        /// <summary>
        /// <para>[GET /updated/query]</para>
        /// <para>Returns series that have changed in a maximum of one week blocks since the provided fromTime.</para>
        /// </summary>
        /// <param name="fromTime">Time to start your date range.</param>        
        /// /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<Update[]>> GetAsync(DateTime fromTime, CancellationToken cancellationToken);

        /// <summary>
        /// <para>[GET /updated/query]</para>
        /// <para>Returns series that have changed in a maximum of one week blocks since the provided fromTime.</para>
        /// <para>Any timespan larger than a week will be reduced down to one week automatically.</para>
        /// </summary>
        /// <param name="fromTime">Time to start your date range.</param>
        /// <param name="toTime">Time to end your date range. Must be one week from fromTime</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<Update[]>> GetAsync(DateTime fromTime, DateTime toTime, CancellationToken cancellationToken);

        /// <summary>
        /// <para>[GET /updated/query]</para>
        /// <para>Returns series that have changed in a maximum of one week blocks since the provided fromTime.</para>
        /// </summary>
        /// <param name="fromTime">Time to start your date range.</param>        
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<Update[]>> GetAsync(DateTime fromTime);

        /// <summary>
        /// <para>[GET /updated/query]</para>
        /// <para>Returns series that have changed in a maximum of one week blocks since the provided fromTime.</para>
        /// <para>Any timespan larger than a week will be reduced down to one week automatically.</para>
        /// </summary>
        /// <param name="fromTime">Time to start your date range.</param>
        /// <param name="toTime">Time to end your date range. Must be one week from fromTime</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<Update[]>> GetAsync(DateTime fromTime, DateTime toTime);
    }
}