namespace TvDbSharper.Clients
{
    using System.Threading;
    using System.Threading.Tasks;

    using TvDbSharper.Dto;
    using TvDbSharper.Infrastructure;

    internal class EpisodesClient : IEpisodesClient
    {
        public EpisodesClient(IApiClient apiClient, IParser parser)
        {
            this.ApiClient = apiClient;
            this.Parser = parser;
        }

        private IApiClient ApiClient { get; }

        private IParser Parser { get; }

        public async Task<TvDbResponse<EpisodeRecord>> GetAsync(int episodeId, CancellationToken cancellationToken)
        {
            var request = new ApiRequest("GET", $"/episodes/{episodeId}");
            var response = await this.ApiClient.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
            return this.Parser.Parse<TvDbResponse<EpisodeRecord>>(response, ErrorMessages.Episodes.GetAsync);
        }

        public Task<TvDbResponse<EpisodeRecord>> GetAsync(int episodeId)
        {
            return this.GetAsync(episodeId, CancellationToken.None);
        }
    }
}