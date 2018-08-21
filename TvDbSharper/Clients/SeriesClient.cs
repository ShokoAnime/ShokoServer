namespace TvDbSharper.Clients
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using TvDbSharper.Dto;
    using TvDbSharper.Infrastructure;

    internal class SeriesClient : ISeriesClient
    {
        public SeriesClient(IApiClient apiClient, IParser parser)
        {
            this.ApiClient = apiClient;
            this.Parser = parser;
            this.UrlHelpers = new UrlHelpers();
        }

        private IApiClient ApiClient { get; }

        private IParser Parser { get; }

        private UrlHelpers UrlHelpers { get; }

        public async Task<TvDbResponse<Actor[]>> GetActorsAsync(int seriesId, CancellationToken cancellationToken)
        {
            var request = new ApiRequest("GET", $"/series/{seriesId}/actors");
            var response = await this.ApiClient.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
            return this.Parser.Parse<TvDbResponse<Actor[]>>(response, ErrorMessages.Series.GetAsync);
        }

        public Task<TvDbResponse<Actor[]>> GetActorsAsync(int seriesId)
        {
            return this.GetActorsAsync(seriesId, CancellationToken.None);
        }

        public async Task<TvDbResponse<Series>> GetAsync(int seriesId, SeriesFilter filter, CancellationToken cancellationToken)
        {
            var request = new ApiRequest("GET", $"/series/{seriesId}/filter?keys={this.UrlHelpers.Parametrify(filter)}");
            var response = await this.ApiClient.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
            return this.Parser.Parse<TvDbResponse<Series>>(response, ErrorMessages.Series.GetAsync);
        }

        public async Task<TvDbResponse<Series>> GetAsync(int seriesId, CancellationToken cancellationToken)
        {
            var request = new ApiRequest("GET", $"/series/{seriesId}");
            var response = await this.ApiClient.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
            return this.Parser.Parse<TvDbResponse<Series>>(response, ErrorMessages.Series.GetAsync);
        }

        public Task<TvDbResponse<Series>> GetAsync(int seriesId)
        {
            return this.GetAsync(seriesId, CancellationToken.None);
        }

        public Task<TvDbResponse<Series>> GetAsync(int seriesId, SeriesFilter filter)
        {
            return this.GetAsync(seriesId, filter, CancellationToken.None);
        }

        public async Task<TvDbResponse<BasicEpisode[]>> GetEpisodesAsync(int seriesId, int page, CancellationToken cancellationToken)
        {
            var request = new ApiRequest("GET", $"/series/{seriesId}/episodes?page={Math.Max(page, 1)}");
            var response = await this.ApiClient.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
            return this.Parser.Parse<TvDbResponse<BasicEpisode[]>>(response, ErrorMessages.Series.GetAsync);
        }

        public async Task<TvDbResponse<BasicEpisode[]>> GetEpisodesAsync(
            int seriesId,
            int page,
            EpisodeQuery query,
            CancellationToken cancellationToken)
        {
            string url = $"/series/{seriesId}/episodes/query?page={Math.Max(page, 1)}&{this.UrlHelpers.Querify(query)}";
            var request = new ApiRequest("GET", url);
            var response = await this.ApiClient.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
            return this.Parser.Parse<TvDbResponse<BasicEpisode[]>>(response, ErrorMessages.Series.GetAsync);
        }

        public Task<TvDbResponse<BasicEpisode[]>> GetEpisodesAsync(int seriesId, int page)
        {
            return this.GetEpisodesAsync(seriesId, page, CancellationToken.None);
        }

        public Task<TvDbResponse<BasicEpisode[]>> GetEpisodesAsync(int seriesId, int page, EpisodeQuery query)
        {
            return this.GetEpisodesAsync(seriesId, page, query, CancellationToken.None);
        }

        public async Task<TvDbResponse<EpisodesSummary>> GetEpisodesSummaryAsync(int seriesId, CancellationToken cancellationToken)
        {
            var request = new ApiRequest("GET", $"/series/{seriesId}/episodes/summary");
            var response = await this.ApiClient.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
            return this.Parser.Parse<TvDbResponse<EpisodesSummary>>(response, ErrorMessages.Series.GetAsync);
        }

        public Task<TvDbResponse<EpisodesSummary>> GetEpisodesSummaryAsync(int seriesId)
        {
            return this.GetEpisodesSummaryAsync(seriesId, CancellationToken.None);
        }

        public async Task<IDictionary<string, string>> GetHeadersAsync(int seriesId, CancellationToken cancellationToken)
        {
            var request = new ApiRequest("HEAD", $"/series/{seriesId}");
            var response = await this.ApiClient.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);

            return response.Headers;
        }

        public Task<IDictionary<string, string>> GetHeadersAsync(int seriesId)
        {
            return this.GetHeadersAsync(seriesId, CancellationToken.None);
        }

        public async Task<TvDbResponse<Image[]>> GetImagesAsync(int seriesId, ImagesQuery query, CancellationToken cancellationToken)
        {
            var request = new ApiRequest("GET", $"/series/{seriesId}/images/query?{this.UrlHelpers.Querify(query)}");
            var response = await this.ApiClient.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
            return this.Parser.Parse<TvDbResponse<Image[]>>(response, ErrorMessages.Series.GetImagesAsync);
        }

        public Task<TvDbResponse<Image[]>> GetImagesAsync(int seriesId, ImagesQuery query)
        {
            return this.GetImagesAsync(seriesId, query, CancellationToken.None);
        }

        public async Task<TvDbResponse<ImagesSummary>> GetImagesSummaryAsync(int seriesId, CancellationToken cancellationToken)
        {
            var request = new ApiRequest("GET", $"/series/{seriesId}/images");
            var response = await this.ApiClient.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
            return this.Parser.Parse<TvDbResponse<ImagesSummary>>(response, ErrorMessages.Series.GetAsync);
        }

        public Task<TvDbResponse<ImagesSummary>> GetImagesSummaryAsync(int seriesId)
        {
            return this.GetImagesSummaryAsync(seriesId, CancellationToken.None);
        }
    }
}