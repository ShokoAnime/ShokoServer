namespace TvDbSharper
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using TvDbSharper.Dto;

    /// <summary>
    /// Used for geting information about a specific series
    /// </summary>
    public interface ISeriesClient
    {
        /// <summary>
        /// <para>[GET /series/{id}/actors]</para>
        /// <para>Returns actors for the given series ID</para>
        /// </summary>
        /// <param name="seriesId">The series ID</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<Actor[]>> GetActorsAsync(int seriesId, CancellationToken cancellationToken);

        /// <summary>
        /// <para>[GET /series/{id}/actors]</para>
        /// <para>Returns actors for the given series ID</para>
        /// </summary>
        /// <param name="seriesId">The series ID</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<Actor[]>> GetActorsAsync(int seriesId);

        /// <summary>
        /// <para>[GET /series/{id}/filter]</para>
        /// <para>Returns a series records about a particular series ID that contains all properties listed in the filter argument.</para>
        /// </summary>
        /// <example>
        /// <code>
        /// class TestClass 
        /// {
        ///     static void Main() 
        ///     {
        ///         var client = new TvDbClient();
        /// 
        ///         await client.Authentication.AuthenticateAsync(...);
        ///         
        ///         var recordsData = await client.Series.GetAsync(42, SeriesFilter.Id | SeriesFilter.SeriesName | SeriesFilter.ImdbId,
        ///                                                        CancellationToken.None);
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <param name="seriesId">The series ID</param>
        /// <param name="filter">The enumeration listing properties to be included in the result.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<Series>> GetAsync(int seriesId, SeriesFilter filter, CancellationToken cancellationToken);

        /// <summary>
        /// <para>[GET /series/{id}]</para>
        /// <para>Returns a series records that contains all information known about a particular series.</para>
        /// </summary>
        /// <param name="seriesId">The series ID</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<Series>> GetAsync(int seriesId, CancellationToken cancellationToken);

        /// <summary>
        /// <para>[GET /series/{id}]</para>
        /// <para>Returns a series records that contains all information known about a particular series.</para>
        /// </summary>
        /// <param name="seriesId">The series ID</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<Series>> GetAsync(int seriesId);

        /// <summary>
        /// <para>[GET /series/{id}/filter]</para>
        /// <para>Returns a series records about a particular series ID that contains all properties listed in the filter argument.</para>
        /// </summary>
        /// <example>
        /// <code>
        /// class TestClass 
        /// {
        ///     static void Main() 
        ///     {
        ///         var client = new TvDbClient();
        /// 
        ///         await client.Authentication.AuthenticateAsync(...);
        ///         
        ///         var recordsData = await client.Series.GetAsync(42, SeriesFilter.Id | SeriesFilter.SeriesName | SeriesFilter.ImdbId);
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <param name="seriesId">The series ID</param>
        /// <param name="filter">The enumeration listing properties to be included in the result.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<Series>> GetAsync(int seriesId, SeriesFilter filter);

        /// <summary>
        /// <para>[GET /series/{id}/episodes]</para>
        /// <para>Returns episode records paginated with 100 results per page.</para>
        /// </summary>
        /// <param name="seriesId">The series ID</param>
        /// <param name="page">The page you want to retrieve.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<BasicEpisode[]>> GetEpisodesAsync(int seriesId, int page, CancellationToken cancellationToken);

        /// <summary>
        /// <para>[GET /series/{id}/episodes/query]</para>
        /// <para>Returns episode records paginated with 100 results per page.</para>
        /// </summary>
        /// <param name="seriesId">The series ID</param>
        /// <param name="page">The page you want to retrieve.</param>
        /// <param name="query">The structure by which the records are queried.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<BasicEpisode[]>> GetEpisodesAsync(int seriesId, int page, EpisodeQuery query, CancellationToken cancellationToken);

        /// <summary>
        /// <para>[GET /series/{id}/episodes]</para>
        /// <para>Returns episode records paginated with 100 results per page.</para>
        /// </summary>
        /// <param name="seriesId">The series ID</param>
        /// <param name="page">The page you want to retrieve.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<BasicEpisode[]>> GetEpisodesAsync(int seriesId, int page);

        /// <summary>
        /// <para>[GET /series/{id}/episodes/query]</para>
        /// <para>Returns episode records paginated with 100 results per page.</para>
        /// </summary>
        /// <param name="seriesId">The series ID</param>
        /// <param name="page">The page you want to retrieve.</param>
        /// <param name="query">The structure by which the records are queried.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<BasicEpisode[]>> GetEpisodesAsync(int seriesId, int page, EpisodeQuery query);

        /// <summary>
        /// <para>[GET /series/{id}/episodes/summary]</para>
        /// <para>Returns a summary of the episodes and seasons available for the series.</para>
        /// <para>Note: Season "0" is for all episodes that are considered to be specials.</para>
        /// </summary>
        /// <param name="seriesId">The series ID</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<EpisodesSummary>> GetEpisodesSummaryAsync(int seriesId, CancellationToken cancellationToken);

        /// <summary>
        /// <para>[GET /series/{id}/episodes/summary]</para>
        /// <para>Returns a summary of the episodes and seasons available for the series.</para>
        /// <para>Note: Season "0" is for all episodes that are considered to be specials.</para>
        /// </summary>
        /// <param name="seriesId">The series ID</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<EpisodesSummary>> GetEpisodesSummaryAsync(int seriesId);

        /// <summary>
        /// <para>[HEAD /series/{id}]</para>
        /// <para>Returns header information only about the given series ID.</para>
        /// </summary>
        /// <param name="seriesId">The series ID</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<IDictionary<string, string>> GetHeadersAsync(int seriesId, CancellationToken cancellationToken);

        /// <summary>
        /// <para>[HEAD /series/{id}]</para>
        /// <para>Returns header information only about the given series ID.</para>
        /// </summary>
        /// <param name="seriesId">The series ID</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<IDictionary<string, string>> GetHeadersAsync(int seriesId);

        /// <summary>
        /// <para>[GET /series/{id}/images/query]</para>
        /// <para>Query images for the given series ID.</para>
        /// </summary>
        /// <param name="seriesId">The series ID</param>
        /// <param name="query">The query.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<Image[]>> GetImagesAsync(int seriesId, ImagesQuery query, CancellationToken cancellationToken);

        /// <summary>
        /// <para>[GET /series/{id}/images/query]</para>
        /// <para>Query images for the given series ID.</para>
        /// </summary>
        /// <param name="seriesId">The series ID</param>
        /// <param name="query">The query.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<Image[]>> GetImagesAsync(int seriesId, ImagesQuery query);

        /// <summary>
        /// <para>[GET /series/{id}/images]</para>
        /// <para>Returns a summary of the images for a particular series</para>
        /// </summary>
        /// <param name="seriesId">The series ID</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<ImagesSummary>> GetImagesSummaryAsync(int seriesId, CancellationToken cancellationToken);

        /// <summary>
        /// <para>[GET /series/{id}/images]</para>
        /// <para>Returns a summary of the images for a particular series</para>
        /// </summary>
        /// <param name="seriesId">The series ID</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<ImagesSummary>> GetImagesSummaryAsync(int seriesId);
    }
}