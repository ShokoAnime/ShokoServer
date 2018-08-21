namespace TvDbSharper.Infrastructure
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using Newtonsoft.Json;

    using TvDbSharper.Dto;

    internal class ApiRequest
    {
        public ApiRequest()
        {
        }

        public ApiRequest(string method, string url)
        {
            this.Method = method;
            this.Url = url;
        }

        public ApiRequest(string method, string url, string body)
        {
            this.Method = method;
            this.Url = url;
            this.Body = body;
        }

        public string Body { get; set; }

        public IDictionary<string, string> Headers { get; set; }

        public string Method { get; set; }

        public string Url { get; set; }
    }

    internal class ApiResponse
    {
        public string Body { get; set; }

        public IDictionary<string, string> Headers { get; set; }

        public int StatusCode { get; set; }
    }

    internal interface IApiClient
    {
        string BaseAddress { get; set; }

        IDictionary<string, string> DefaultRequestHeaders { get; set; }

        Task<ApiResponse> SendRequestAsync(ApiRequest request, CancellationToken cancellationToken);
    }

    internal class ApiClient : IApiClient
    {
        public ApiClient()
        {
            this.DefaultRequestHeaders = new ConcurrentDictionary<string, string>();
        }

        public string BaseAddress { get; set; }

        public IDictionary<string, string> DefaultRequestHeaders { get; set; }

        public async Task<ApiResponse> SendRequestAsync(ApiRequest request, CancellationToken cancellationToken)
        {
            return await GetResponseAsync(await this.CreateRequestAsync(request).ConfigureAwait(false), cancellationToken)
                       .ConfigureAwait(false);
        }

        private static void ApplyHeaders(WebRequest request, IDictionary<string, string> headers)
        {
            request.Headers = new WebHeaderCollection();

            foreach (var pair in headers)
            {
                switch (pair.Key)
                {
                    case "Content-Type" :
                    {
                        request.ContentType = pair.Value;
                        break;
                    }

                    default :
                    {
                        request.Headers[pair.Key] = pair.Value;
                        break;
                    }
                }
            }
        }

        private static IDictionary<string, string> CombineHeaders(params IDictionary<string, string>[] headerCollections)
        {
            var result = new Dictionary<string, string>();

            for (var i = 0; i < headerCollections.Length; i++)
            {
                var headerCollection = headerCollections[i];

                if (headerCollection != null)
                {
                    foreach (var pair in headerCollection)
                    {
                        result[pair.Key] = pair.Value;
                    }
                }
            }

            return result;
        }

        private static async Task<ApiResponse> GetResponseAsync(WebRequest httpRequest, CancellationToken cancellationToken)
        {
            HttpWebResponse httpResponse;

            try
            {
                httpResponse = (HttpWebResponse)await httpRequest.GetResponseAsync().ConfigureAwait(false);
            }
            catch (WebException ex)
            {
                httpResponse = (HttpWebResponse)ex.Response;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var stream = httpResponse.GetResponseStream();

            string body;

            using (var reader = new StreamReader(stream))
            {
                body = await reader.ReadToEndAsync().ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();

            var headers = ParseHeaders(httpResponse.Headers);

            return new ApiResponse
            {
                Body = body,
                StatusCode = (int)httpResponse.StatusCode,
                Headers = headers
            };
        }

        private static IDictionary<string, string> ParseHeaders(WebHeaderCollection headers)
        {
            var dict = new Dictionary<string, string>();

            foreach (string name in headers)
            {
                dict[name] = headers[name];
            }

            return dict;
        }

        private async Task<HttpWebRequest> CreateRequestAsync(ApiRequest request)
        {
            string url = (this.BaseAddress ?? string.Empty) + request.Url;

            var httpRequest = WebRequest.CreateHttp(url);
            httpRequest.Method = request.Method;

            ApplyHeaders(httpRequest, CombineHeaders(this.DefaultRequestHeaders, request.Headers));

            if (request.Body != null)
            {
                var stream = await httpRequest.GetRequestStreamAsync().ConfigureAwait(false);

                byte[] buffer = Encoding.UTF8.GetBytes(request.Body);

                await stream.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
            }

            return httpRequest;
        }
    }

    internal interface IParser
    {
        T Parse<T>(ApiResponse response, IReadOnlyDictionary<int, string> errorMap);
    }

    internal class Parser : IParser
    {
        private const string UnknownErrorMessage = "The REST API returned an unkown error.";

        public T Parse<T>(ApiResponse response, IReadOnlyDictionary<int, string> errorMap)
        {
            if (response.StatusCode == 200)
            {
                return JsonConvert.DeserializeObject<T>(response.Body);
            }

            throw CreateException(response, errorMap);
        }

        private static TvDbServerException CreateException(ApiResponse response, IReadOnlyDictionary<int, string> errorMap)
        {
            var messages = new List<string>();

            if (errorMap.ContainsKey(response.StatusCode))
            {
                messages.Add(errorMap[response.StatusCode]);
            }

            string bodyMessage = ReadErrorMessage(response.Body);

            if (bodyMessage != null)
            {
                messages.Add(bodyMessage);
            }

            bool unknownError = !messages.Any();

            if (unknownError)
            {
                messages.Add(UnknownErrorMessage);
            }

            string message = string.Join("; ", messages);

            var exception = new TvDbServerException(message, response.StatusCode)
            {
                UnknownError = unknownError
            };

            return exception;
        }

        private static string ReadErrorMessage(string body)
        {
            try
            {
                return JsonConvert.DeserializeObject<ErrorResponse>(body).Error;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}