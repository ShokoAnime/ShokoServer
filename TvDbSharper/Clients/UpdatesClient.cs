namespace TvDbSharper.Clients
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using TvDbSharper.Dto;
    using TvDbSharper.Infrastructure;

    internal class UpdatesClient : IUpdatesClient
    {
        public UpdatesClient(IApiClient apiClient, IParser parser)
        {
            this.ApiClient = apiClient;
            this.Parser = parser;
        }

        private IApiClient ApiClient { get; }

        private IParser Parser { get; }

        public async Task<TvDbResponse<Update[]>> GetAsync(DateTime fromTime, CancellationToken cancellationToken)
        {
            var request = new ApiRequest("GET", $"/updated/query?fromTime={fromTime.ToUnixEpochTime()}");
            var response = await this.ApiClient.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
            return this.Parser.Parse<TvDbResponse<Update[]>>(response, ErrorMessages.Updates.GetAsync);
        }

        public async Task<TvDbResponse<Update[]>> GetAsync(DateTime fromTime, DateTime toTime, CancellationToken cancellationToken)
        {
            var request = new ApiRequest("GET", $"/updated/query?fromTime={fromTime.ToUnixEpochTime()}&toTime={toTime.ToUnixEpochTime()}");
            var response = await this.ApiClient.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
            return this.Parser.Parse<TvDbResponse<Update[]>>(response, ErrorMessages.Updates.GetAsync);
        }

        public Task<TvDbResponse<Update[]>> GetAsync(DateTime fromTime)
        {
            return this.GetAsync(fromTime, CancellationToken.None);
        }

        public Task<TvDbResponse<Update[]>> GetAsync(DateTime fromTime, DateTime toTime)
        {
            return this.GetAsync(fromTime, toTime, CancellationToken.None);
        }
    }
}