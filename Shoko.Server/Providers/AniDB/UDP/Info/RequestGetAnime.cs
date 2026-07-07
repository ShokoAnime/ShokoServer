using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;

namespace Shoko.Server.Providers.AniDB.UDP.Info;

public class RequestGetAnime : UDPRequest<ResponseGetAnime>
{
    public int AnimeID { get; set; }

    protected override string BaseCommand => $"ANIME aid={AnimeID}&amask=8c000000000000";

    protected internal override UDPResponse<ResponseGetAnime> ParseResponse(UDPResponse<string> response)
    {
        var code = response.Code;
        var receivedData = response.Response;
        switch (code)
        {
            case UDPReturnCode.ANIME:
            {
                var parts = receivedData.Split('|').Select(a => a.Trim()).ToArray();
                if (!int.TryParse(parts[0], out var aid))
                    throw new UnexpectedUDPResponseException("Anime ID was not an int", code, receivedData, Command);

                var relations = new List<ResponseGetAnime.Relation>();
                if (parts.Length >= 3)
                {
                    var aidList = parts[1].Split('\'', StringSplitOptions.RemoveEmptyEntries);
                    var typeList = parts[2].Split('\'', StringSplitOptions.RemoveEmptyEntries);
                    var count = Math.Min(aidList.Length, typeList.Length);
                    for (var i = 0; i < count; i++)
                    {
                        if (!int.TryParse(aidList[i], out var relatedAid))
                            continue;
                        if (!int.TryParse(typeList[i], out var rawType))
                            continue;
                        relations.Add(new ResponseGetAnime.Relation { RelatedAnimeID = relatedAid, RawType = rawType });
                    }
                }

                return new UDPResponse<ResponseGetAnime>
                {
                    Code = code,
                    Response = new ResponseGetAnime { AnimeID = aid, Relations = relations }
                };
            }
            case UDPReturnCode.NO_SUCH_ANIME:
            {
                return new UDPResponse<ResponseGetAnime> { Code = code, Response = null! };
            }
            default:
                throw new UnexpectedUDPResponseException(code, receivedData, Command);
        }
    }

    public RequestGetAnime(ILoggerFactory loggerFactory, IUDPConnectionHandler handler) : base(loggerFactory, handler)
    {
    }
}
