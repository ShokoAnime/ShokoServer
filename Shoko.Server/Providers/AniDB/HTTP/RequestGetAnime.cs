using System;
using AniDBAPI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.Providers.AniDB.Http
{
    public class RequestGetAnime : HttpBaseRequest<ResponseGetAnime>
    {
        public int AnimeID { get; init; }

        protected override string BaseCommand => $"http://api.anidb.net:9001/httpapi?client=animeplugin&clientver=1&protover=1&request=anime&aid={AnimeID}";

        protected override HttpBaseResponse<ResponseGetAnime> ParseResponse(ILogger logger, HttpBaseResponse<string> receivedData)
        {
            // this won't be called. It is bypassed in the version with a service provider
            throw new NotSupportedException();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="provider"></param>
        /// <param name="receivedData"></param>
        /// <returns></returns>
        /// <exception cref="AniDBBannedException">Will throw if banned. Won't extend ban, so it's safe to use this as a check</exception>
        protected override HttpBaseResponse<ResponseGetAnime> ParseResponse(IServiceProvider provider, HttpBaseResponse<string> receivedData)
        {
            var xmlUtils = provider.GetRequiredService<HttpXmlUtils>();
            UpdateAnimeUpdateTime(AnimeID);

            // save a file cache of the response
            var rawXml = receivedData.Response.Trim();
            xmlUtils.WriteAnimeHTTPToFile(AnimeID, rawXml);

            var parser = provider.GetRequiredService<HttpParser>();
            var response = parser.Parse(AnimeID, receivedData.Response);
            return new HttpBaseResponse<ResponseGetAnime> { Code = receivedData.Code, Response = response };
        }

        private static void UpdateAnimeUpdateTime(int animeId)
        {
            // Putting this here for no chance of error. It is ALWAYS created or updated when AniDB is called!
            var anime = RepoFactory.AniDB_AnimeUpdate.GetByAnimeID(animeId);
            if (anime == null)
                anime = new AniDB_AnimeUpdate { AnimeID = animeId, UpdatedAt = DateTime.Now };
            else
                anime.UpdatedAt = DateTime.Now;
            RepoFactory.AniDB_AnimeUpdate.Save(anime);
        }
    }
}
