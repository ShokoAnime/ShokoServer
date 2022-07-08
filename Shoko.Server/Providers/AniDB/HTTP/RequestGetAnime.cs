using System;
using System.Xml;
using AniDBAPI;
using Microsoft.Extensions.Logging;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.Providers.AniDB.Http
{
    public class RequestGetAnime : HttpBaseRequest<ResponseGetAnime>
    {
        public int AnimeID { get; init; }

        protected override string BaseCommand => $"http://api.anidb.net:9001/httpapi?client=animeplugin&clientver=1&protover=1&request=anime&aid={AnimeID}";

        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="receivedData"></param>
        /// <returns></returns>
        /// <exception cref="AniDBBannedException">Will throw if banned. Won't extend ban, so it's safe to use this as a check</exception>
        protected override HttpBaseResponse<ResponseGetAnime> ParseResponse(ILogger logger, HttpBaseResponse<string> receivedData)
        {
            // TODO move a lot of the interdependent parts to more modular pieces with interfaces
            // For now, this is just a 1 to 1 logic move
            UpdateAnimeUpdateTime(AnimeID);
            
            XmlDocument docAnime = null;
            var rawXml = receivedData.Response.Trim();
            if (rawXml.Length > 0)
            {
                APIUtils.WriteAnimeHTTPToFile(AnimeID, rawXml);

                docAnime = new XmlDocument();
                docAnime.LoadXml(rawXml);
            }
            else
            {
                logger.LogWarning("When downloading anime data for {AnimeID}, the xml response could not be read", AnimeID);
            }

            return new HttpBaseResponse<ResponseGetAnime>();
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
