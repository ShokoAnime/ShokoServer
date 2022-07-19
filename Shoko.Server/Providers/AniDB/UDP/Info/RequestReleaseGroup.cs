using System.Linq;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;

namespace Shoko.Server.Providers.AniDB.UDP.Info
{
    public class RequestReleaseGroup : UDPRequest<ResponseReleaseGroup>
    {
        public int ReleaseGroupID { get; set; }

        
        protected override string BaseCommand => $"GROUP gid={ReleaseGroupID}";

        protected override UDPResponse<ResponseReleaseGroup> ParseResponse(ILogger logger, UDPResponse<string> response)
        {
            var code = response.Code;
            var receivedData = response.Response;
            switch (code)
            {
                case UDPReturnCode.GROUP:
                {
                    // {int gid}|{int4 rating}|{int votes}|{int4 acount}|{int fcount}|{str name}|{str short}|{str irc channel}|{str irc server}|{str url}|{str picname}|{int4 foundeddate}|{int4 disbandeddate}|{int2 dateflags}|{int4 lastreleasedate}|{int4 lastactivitydate}|{list grouprelations}
                    /*
                        dateflags values:
                        bit0 set == Foundeddate, Unknown Day
                        bit1 set == Foundeddate, Unknown Month, Day
                        bit2 set == Disbandeddate, Unknown Day
                        bit3 set == Disbandeddate, Unknown Month, Day
                        bit5 set == Foundeddate, Unknown Year
                        bit6 set == Disbandeddate, Unknown Year
                        releasedate and activitydate are distinct. releasedate is the date a file was actually released by the group, where activitydate is the date of a file being added to AniDB. As such, lastrelease may very well be much older than lastactivity.
                        groupreleations is a list of apostrophe-separated pairs, where each pair consists of {int4 othergroupid},{int2 relationtype}
                        relationtype:
                        1 => "Participant in"
                        2 => "Parent of"
                        4 => "Merged from"
                        5 => "Now known as"
                        6 => "Other"
                     */
                    string[] parts = receivedData.Split('|').Select(a => a.Trim()).ToArray();
                    if (!int.TryParse(parts[0], out int gid)) throw new UnexpectedUDPResponseException("Group ID was not an int", code, receivedData);
                    if (!int.TryParse(parts[1], out int intRating)) throw new UnexpectedUDPResponseException("Rating was not an int", code, receivedData);
                    decimal rating = intRating / 100M;
                    if (!int.TryParse(parts[2], out int votes)) throw new UnexpectedUDPResponseException("Votes was not an int", code, receivedData);
                    if (!int.TryParse(parts[3], out int aCount)) throw new UnexpectedUDPResponseException("Anime Count was not an int", code, receivedData);
                    if (!int.TryParse(parts[4], out int fCount)) throw new UnexpectedUDPResponseException("File Count was not an int", code, receivedData);
                    var name = parts[5];
                    var shortName = parts[6];
                    var ircChannel = parts[7];
                    var ircServer = parts[8];
                    var url = parts[9];
                    var pic = parts[10];

                    return new UDPResponse<ResponseReleaseGroup>() {Code = code, Response = new ResponseReleaseGroup
                    {
                        ID = gid,
                        Rating = rating,
                        Votes = votes,
                        AnimeCount = aCount,
                        FileCount = fCount,
                        Name = name,
                        ShortName = shortName,
                        IrcChannel = ircChannel,
                        IrcServer = ircServer,
                        URL = url,
                        Picture = pic
                    }};
                }
                case UDPReturnCode.NO_SUCH_GROUP:
                {
                    return new UDPResponse<ResponseReleaseGroup>() {Code = code, Response = null};
                }
                default: throw new UnexpectedUDPResponseException(code, receivedData);
            }
        }
    }
}
