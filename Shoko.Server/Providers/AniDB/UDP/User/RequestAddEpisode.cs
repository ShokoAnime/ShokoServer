using System;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;

namespace Shoko.Server.Providers.AniDB.UDP.User
{
    public class RequestAddEpisode : UDPRequest<ResponseMyListFile>
    {
        protected override string BaseCommand
        {
            get
            {
                var type = "";
                if (EpisodeType != EpisodeType.Episode)
                    type = EpisodeType.ToString()[..1];
                var command = $"MYLISTADD aid={AnimeID}&epno={type+EpisodeNumber}&generic=1&state={State}";
                if (IsWatched)
                {
                    var date = WatchedDate ?? DateTime.Now;
                    command += $"&viewed=1&viewdate={Commons.Utils.AniDB.GetAniDBDateAsSeconds(date)}";
                }
                else
                {
                    command += "viewed=0";
                }

                return command;
            }
        }

        public int AnimeID { get; set; }

        public int EpisodeNumber { get; set; }
        public EpisodeType EpisodeType { get; set; } = EpisodeType.Episode;

        public MyList_State State { get; set; }

        public bool IsWatched { get; set; }
        public DateTime? WatchedDate { get; set; }

        protected override UDPResponse<ResponseMyListFile> ParseResponse(ILogger logger, UDPResponse<string> response)
        {
            var code = response.Code;
            var receivedData = response.Response;
            switch (code)
            {
                case UDPReturnCode.MYLIST_ENTRY_ADDED:
                {
                    // We're adding a generic file, so it won't return a MyListID
                    return new UDPResponse<ResponseMyListFile>
                    {
                        Code = code,
                        Response = new ResponseMyListFile
                        {
                            State = State,
                            IsWatched = IsWatched,
                            WatchedDate = WatchedDate,
                            UpdatedAt = DateTime.Now,
                        },
                    };
                }
                case UDPReturnCode.FILE_ALREADY_IN_MYLIST:
                {
                    /* Response Format
                     * {int4 lid}|{int4 fid}|{int4 eid}|{int4 aid}|{int4 gid}|{int4 date}|{int2 state}|{int4 viewdate}|{str storage}|{str source}|{str other}|{int2 filestate}
                     */
                    //file already exists: read 'watched' status
                    var arrResult = receivedData.Split('\n');
                    if (arrResult.Length >= 2)
                    {
                        var arrStatus = arrResult[1].Split('|');
                        // We expect 0 for a MyListID
                        int.TryParse(arrStatus[0], out var myListID);

                        var state = (MyList_State) int.Parse(arrStatus[6]);

                        var viewdate = int.Parse(arrStatus[7]);
                        var updatedate = int.Parse(arrStatus[5]);
                        var watched = viewdate > 0;
                        DateTime? updatedAt = null;
                        DateTime? watchedDate = null;
                        if (updatedate > 0)
                            updatedAt = DateTime.UnixEpoch
                            .AddSeconds(updatedate)
                            .ToLocalTime();
                        if (watched)
                            watchedDate = DateTime.UnixEpoch
                                .AddSeconds(viewdate)
                                .ToLocalTime();

                        return new UDPResponse<ResponseMyListFile>
                        {
                            Code = code,
                            Response = new ResponseMyListFile
                            {
                                MyListID = myListID,
                                State = state,
                                IsWatched = watched,
                                WatchedDate = watchedDate,
                                UpdatedAt = updatedAt,
                            },
                        };
                    }
                    break;
                }
            }
            throw new UnexpectedUDPResponseException(code, receivedData);
        }
    }
}
