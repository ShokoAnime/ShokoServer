namespace TvDbSharper.Clients
{
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    using TvDbSharper.Dto;
    using TvDbSharper.Infrastructure;

    internal class SearchClient : ISearchClient
    {
        public SearchClient(IApiClient apiClient, IParser parser)
        {
            this.ApiClient = apiClient;
            this.Parser = parser;
            this.UrlHelpers = new UrlHelpers();
        }

        private IApiClient ApiClient { get; }

        private IParser Parser { get; }

        private UrlHelpers UrlHelpers { get; }

        public async Task<TvDbResponse<SeriesSearchResult[]>> SearchSeriesAsync(
            string value,
            SearchParameter parameter,
            CancellationToken cancellationToken)
        {
            string url = $"/search/series?{this.UrlHelpers.PascalCase(parameter.ToString())}={WebUtility.UrlEncode(value)}";
            var request = new ApiRequest("GET", url);
            var response = await this.ApiClient.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
            return this.Parser.Parse<TvDbResponse<SeriesSearchResult[]>>(response, ErrorMessages.Search.SearchSeriesAsync);
        }

        public Task<TvDbResponse<SeriesSearchResult[]>> SearchSeriesAsync(string value, SearchParameter parameterKey)
        {
            return this.SearchSeriesAsync(value, parameterKey, CancellationToken.None);
        }

        public Task<TvDbResponse<SeriesSearchResult[]>> SearchSeriesByImdbIdAsync(string imdbId, CancellationToken cancellationToken)
        {
            return this.SearchSeriesAsync(imdbId, SearchParameter.ImdbId, cancellationToken);
        }

        public Task<TvDbResponse<SeriesSearchResult[]>> SearchSeriesByImdbIdAsync(string imdbId)
        {
            return this.SearchSeriesByImdbIdAsync(imdbId, CancellationToken.None);
        }

        public Task<TvDbResponse<SeriesSearchResult[]>> SearchSeriesByNameAsync(string name, CancellationToken cancellationToken)
        {
            return this.SearchSeriesAsync(name, SearchParameter.Name, cancellationToken);
        }

        public Task<TvDbResponse<SeriesSearchResult[]>> SearchSeriesByNameAsync(string name)
        {
            return this.SearchSeriesByNameAsync(name, CancellationToken.None);
        }

        public Task<TvDbResponse<SeriesSearchResult[]>> SearchSeriesByZap2ItIdAsync(string zap2ItId, CancellationToken cancellationToken)
        {
            return this.SearchSeriesAsync(zap2ItId, SearchParameter.Zap2itId, cancellationToken);
        }

        public Task<TvDbResponse<SeriesSearchResult[]>> SearchSeriesByZap2ItIdAsync(string zap2ItId)
        {
            return this.SearchSeriesByZap2ItIdAsync(zap2ItId, CancellationToken.None);
        }
    }
}