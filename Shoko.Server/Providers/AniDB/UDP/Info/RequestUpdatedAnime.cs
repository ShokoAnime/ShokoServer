using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.UDP.Generic;

namespace Shoko.Server.Providers.AniDB.UDP.Info
{
    public class RequestUpdatedAnime : UDPRequest<ResponseUpdatedAnime>
    {
        public DateTime LastUpdated { get; set; }
        protected override string BaseCommand => $"UPDATED entity=1&time={Commons.Utils.AniDB.GetAniDBDateAsSeconds(LastUpdated)}";

        protected override UDPResponse<ResponseUpdatedAnime> ParseResponse(ILogger logger, UDPResponse<string> response)
        {
            var code = response.Code;
            if (code != UDPReturnCode.UPDATED) return new UDPResponse<ResponseUpdatedAnime> { Code = code, Response = null };
            var fields = response.Response.Split('|');
            var result = new ResponseUpdatedAnime { Count = int.Parse(fields[1]), LastUpdated = DateTime.UnixEpoch.AddSeconds(long.Parse(fields[2])), AnimeIDs = fields[3].Trim().Split(',').Select(int.Parse).ToList() };
            return new UDPResponse<ResponseUpdatedAnime> { Code = code, Response = result };
        }
    }
}
