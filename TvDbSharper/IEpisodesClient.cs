namespace TvDbSharper
{
    using System.Threading;
    using System.Threading.Tasks;

    using TvDbSharper.Dto;

    /// <summary>
    /// Used for getting information about a specific episode
    /// </summary>
    public interface IEpisodesClient
    {
        /// <summary>
        /// <para>[GET /episodes/{id}]</para>
        /// <para>Returns the full information for a given episode ID.</para>
        /// </summary>
        /// <param name="episodeId">The episode ID</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<EpisodeRecord>> GetAsync(int episodeId, CancellationToken cancellationToken);

        /// <summary>
        /// <para>[GET /episodes/{id}]</para>
        /// <para>Returns the full information for a given episode ID.</para>
        /// </summary>
        /// <param name="episodeId">The episode ID</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<EpisodeRecord>> GetAsync(int episodeId);
    }
}