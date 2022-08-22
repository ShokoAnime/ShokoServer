using System.Linq;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;

namespace Shoko.Server.Providers.AniDB.UDP.Info
{
    /// <summary>
    /// Get File Info. Getting the file info will only return any data if the hashes match
    /// If there is MyList info, it will also return that
    /// </summary>
    public class RequestGetEpisode : UDPRequest<ResponseGetEpisode>
    {
        // These are dependent on context
        protected override string BaseCommand => $"Episode eid={EpisodeID}";

        public int EpisodeID { get; set; }

        protected override UDPResponse<ResponseGetEpisode> ParseResponse(UDPResponse<string> response)
        {
            var code = response.Code;
            var receivedData = response.Response;
            switch (code)
            {
                case UDPReturnCode.EPISODE:
                {
                    // {int eid}|{int aid}|{int4 length}|{int4 rating}|{int votes}|{str epno}|{str eng}|{str romaji}|{str kanji}|{int aired}|{int type}
                    // we aren't going to bother with most of this. we get it from the file and anime records
                    var parts = receivedData.Split('|').Select(a => a.Trim()).ToArray();
                    if (parts.Length < 2) throw new UnexpectedUDPResponseException("There were the wrong number of data columns", code, receivedData);
                    // parse out numbers into temp vars
                    if (!int.TryParse(parts[0], out var eid)) throw new UnexpectedUDPResponseException("Episode ID was not an int", code, receivedData);
                    if (!int.TryParse(parts[1], out var aid)) throw new UnexpectedUDPResponseException("Anime ID was not an int", code, receivedData);

                    return new UDPResponse<ResponseGetEpisode> { Code = code, Response = new ResponseGetEpisode { AnimeID = aid, EpisodeID = eid } };
                }
            }
            throw new UnexpectedUDPResponseException(code, receivedData);
        }

        public RequestGetEpisode(ILoggerFactory loggerFactory, IUDPConnectionHandler handler) : base(loggerFactory, handler)
        {
        }
    }
}
