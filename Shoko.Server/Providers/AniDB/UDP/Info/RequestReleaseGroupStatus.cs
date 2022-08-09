using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;

namespace Shoko.Server.Providers.AniDB.UDP.Info
{
    public class RequestReleaseGroupStatus : UDPRequest<List<ResponseReleaseGroupStatus>>
    {
        public int AnimeID { get; set; }

        protected override string BaseCommand => $"GROUPSTATUS aid={AnimeID}";

        protected override UDPResponse<List<ResponseReleaseGroupStatus>> ParseResponse(ILogger logger, UDPResponse<string> response)
        {
            var code = response.Code;
            var receivedData = response.Response;
            switch (code)
            {
                case UDPReturnCode.GROUP_STATUS:
                    /*
			        // 225 GROUPSTATUS
			        1612|MDAN|1|9|784|2|1-9
			        1412|Dattebayo|1|9|677|55|1-9
			        6371|Hatsuyuki Fansub|1|9|738|4|1-9
			        5900|Shiroi-Fansubs|1|9|645|3|1-9
			        6897|Black Ocean Team|1|8|0|0|1-8
			        7209|Otaku Trad Team|1|8|0|0|1-8
			        5816|ALanime Fansub|1|7|836|2|1-7
			        1472|Saikou-BR|1|6|0|0|1-6
			        6638|Yuurisan-Subs & Shinsen-Subs|1|5|674|12|1-5
			        7624|Desire & Himmel & Inmeliora|1|5|657|15|1-5
			        2777|S?ai`No`Naka|1|5|867|1|1-5
			        5618|Yuurisan-Subs|1|5|594|4|1-5
			        6738|AnimeManganTR|1|5|0|0|1-5
			        7673|PA-Fansub|1|4|0|0|1-4
			        7512|Anime Brat|1|3|0|0|1-3
			        7560|Demon Sub|1|3|0|0|1-3
			        6197|Funny and Fantasy subs|1|2|896|1|1-2
			        7887|Yaoi Daisuki no Fansub & Sleepless Beauty no Fansub|1|7|0|0|5,7
			        7466|Aasasubs Clique|1|1|578|4|1
			        7429|Inter-Anime Fansub|1|1|656|1|1
			        6358|Aino Fansub|1|8|0|0|8
			        6656|Atelier Thryst|1|1|747|13|1
			        */
                    var groups = new List<ResponseReleaseGroupStatus>();

                    // remove the header info
                    var sDetails = response.Response.Split('\n');

                    if (sDetails.Length <= 2) throw new UnexpectedUDPResponseException("The number of lines was less than expected", code, receivedData);

                    // first item will be the status command, and last will be empty
                    foreach (var t in sDetails)
                    {
                        var flds = t.Split('|');
                        if (flds.Length != 7) continue;
                        try
                        {
                            // {int group id}|{str group name}|{int completion state}|{int last episode number}|{int rating}|{int votes}|{str episode range}\n
                            var ranges = flds[6].Split(',');
                            var episodes = ranges.SelectMany(
                                a =>
                                {
                                    if (!a.Contains('-')) return new[] { a };
                                    var range = a.Split('-');
                                    if (range.Length != 2) return Array.Empty<string>();
                                    if (int.TryParse(range[0], out var start) && int.TryParse(range[1], out var end))
                                        return Enumerable.Range(start, end - start + 1).Select(b => b.ToString());
                                    if (int.TryParse(range[0][1..], out start) && int.TryParse(range[1][1..], out end))
                                        return Enumerable.Range(start, end - start + 1).Select(b => range[0][0] + b.ToString());
                                    return Array.Empty<string>();
                                }
                            ).ToList();
                            var grp = new ResponseReleaseGroupStatus
                            {
                                AnimeID = AnimeID,
                                GroupID = int.Parse(flds[0]),
                                GroupName = flds[1],
                                CompletionState = (Group_CompletionStatus) int.Parse(flds[2]),
                                LastEpisodeNumber = int.Parse(flds[3]),
                                Rating = int.Parse(flds[4]) / 100M,
                                Votes = int.Parse(flds[5]),
                                ReleasedEpisodes = episodes,
                            };

                            groups.Add(grp);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "{Ex}", ex.ToString());
                        }
                    }

                    return new UDPResponse<List<ResponseReleaseGroupStatus>> { Code = response.Code, Response = groups };
                case UDPReturnCode.NO_SUCH_ANIME:
                    return new UDPResponse<List<ResponseReleaseGroupStatus>> { Code = response.Code, Response = null };
                case UDPReturnCode.NO_GROUPS_FOUND:
                    return new UDPResponse<List<ResponseReleaseGroupStatus>> { Code = response.Code, Response = null };
                default: throw new UnexpectedUDPResponseException(code, receivedData);
            }
        }
    }
}
