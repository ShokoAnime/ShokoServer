using System.Linq;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.UDP.Generic;

namespace Shoko.Server.Providers.AniDB.UDP.User
{
    public class RequestMyListStats : UDPBaseRequest<ResponseMyListStats>
    {
        protected override string BaseCommand => "MYLISTSTATS";
        protected override UDPBaseResponse<ResponseMyListStats> ParseResponse(ILogger logger, UDPBaseResponse<string> response)
        {
            var code = response.Code;
            var receivedData = response.Response;
            var parsedData = receivedData.Split('|').Select(a => !long.TryParse(a.Trim(), out var result) ? 0 : result)
                .ToArray();

            // 222 MYLIST STATS
            // 281|3539|4025|1509124|0|0|0|0|100|100|0|3|5|170|23|0|4001

            var stats = new ResponseMyListStats
            {
                Anime = (int) parsedData[0],
                Episodes = (int) parsedData[1],
                Files = (int) parsedData[2],
                SizeOfFiles = parsedData[3],
                AddedAnime = (int) parsedData[4],
                AddedEpisodes = (int) parsedData[5],
                AddedFiles = (int) parsedData[6],
                AddedGroups = (int) parsedData[7],
                LeechPercent = (int) parsedData[8],
                GloryPercent = (int) parsedData[9],
                ViewedPercent = (int) parsedData[10],
                MyListPercent = (int) parsedData[11],
                ViewedMyListPercent = (int) parsedData[12],
                EpisodesViewed = (int) parsedData[13],
                Votes = (int) parsedData[14],
                Reviews = (int) parsedData[15],
                ViewedLength = parsedData[16]
            };
            return new UDPBaseResponse<ResponseMyListStats>{Code = code, Response = stats};
        }
    }
}
