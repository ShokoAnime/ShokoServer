namespace TvDbSharper
{
    using System.Threading;
    using System.Threading.Tasks;

    using TvDbSharper.Dto;

    /// <summary>
    /// Used for searching for a particular series by name, imdb ID or Zap2It ID
    /// </summary>
    public interface ISearchClient
    {
        /// <summary>
        /// <para>[GET /search/series]</para>
        /// <para>Returns a series search result based on the following parameters.</para>
        /// </summary>
        /// <param name="value">The parameter value</param>
        /// <param name="parameterKey">An enum used for searching for series with <see cref="T:ISearchClient.SearchSeriesAsync"/>,
        /// each value represents a property by which the search is performed</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<SeriesSearchResult[]>> SearchSeriesAsync(
            string value,
            SearchParameter parameterKey,
            CancellationToken cancellationToken);

        /// <summary>
        /// <para>[GET /search/series]</para>
        /// <para>Returns a series search result based on the following parameters</para>
        /// </summary>
        /// <param name="value">The parameter value</param>
        /// <param name="parameterKey">An enum used for searching for series with <see cref="T:ISearchClient.SearchSeriesAsync"/>,
        /// each value represents a property by which the search is performed</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<SeriesSearchResult[]>> SearchSeriesAsync(string value, SearchParameter parameterKey);

        /// <summary>
        /// <para>[GET /search/series]</para>
        /// <para>Returns a series search result based on their imdb ID</para>
        /// </summary>
        /// <param name="imdbId">The imdb ID of the series</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<SeriesSearchResult[]>> SearchSeriesByImdbIdAsync(string imdbId, CancellationToken cancellationToken);

        /// <summary>
        /// <para>[GET /search/series]</para>
        /// <para>Returns a series search result based on their imdb ID</para>
        /// </summary>
        /// <param name="imdbId">The imdb ID of the series</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<SeriesSearchResult[]>> SearchSeriesByImdbIdAsync(string imdbId);

        /// <summary>
        /// <para>[GET /search/series]</para>
        /// <para>Returns a series search result based on their name</para>
        /// </summary>
        /// <param name="name">The series name</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<SeriesSearchResult[]>> SearchSeriesByNameAsync(string name, CancellationToken cancellationToken);

        /// <summary>
        /// <para>[GET /search/series]</para>
        /// <para>Returns a series search result based on their name</para>
        /// </summary>
        /// <param name="name">The series name</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<SeriesSearchResult[]>> SearchSeriesByNameAsync(string name);

        /// <summary>
        /// <para>[GET /search/series]</para>
        /// <para>Returns a series search result based on their Zap2It ID</para>
        /// </summary>
        /// <param name="zap2ItId">The Zap2It ID of the series</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<SeriesSearchResult[]>> SearchSeriesByZap2ItIdAsync(string zap2ItId, CancellationToken cancellationToken);

        /// <summary>
        /// <para>[GET /search/series]</para>
        /// <para>Returns a series search result based on their Zap2It ID</para>
        /// </summary>
        /// <param name="zap2ItId">The Zap2It ID of the series</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<SeriesSearchResult[]>> SearchSeriesByZap2ItIdAsync(string zap2ItId);
    }
}