using System;
using Shoko.Models.Enums;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.MyList.Responses;
using Shoko.Server.Providers.AniDB.UDP.Requests;
using Shoko.Server.Providers.AniDB.UDP.Responses;

namespace Shoko.Server.Providers.AniDB.UDP.MyList.Requests
{
    public class AniDBMyList_RequestAddEpisode : AniDBUDP_BaseRequest<AniDBMyList_ResponseAddFile>
    {
        protected override string BaseCommand
        {
            get
            {
                string command = $"MYLISTADD aid={AnimeID}&epno={EpisodeNumber}&generic=1&state={State}";
                if (IsWatched)
                {
                    DateTime date = WatchedDate ?? DateTime.Now;
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

        public AniDBFile_State State { get; set; }

        public bool IsWatched { get; set; }
        public DateTime? WatchedDate { get; set; }

        protected override AniDBUDP_Response<AniDBMyList_ResponseAddFile> ParseResponse(AniDBUDPReturnCode code, string receivedData)
        {
            switch (code)
            {
                case AniDBUDPReturnCode.MYLIST_ENTRY_ADDED:
                {
                    // We're adding a generic file, so it won't return a MyListID
                    return new AniDBUDP_Response<AniDBMyList_ResponseAddFile>
                    {
                        Code = code,
                        Response = new AniDBMyList_ResponseAddFile
                        {
                            State = State,
                            IsWatched = IsWatched,
                            WatchedDate = WatchedDate
                        }
                    };
                }
                case AniDBUDPReturnCode.FILE_ALREADY_IN_MYLIST:
                {
                    /* Response Format
                     * {int4 lid}|{int4 fid}|{int4 eid}|{int4 aid}|{int4 gid}|{int4 date}|{int2 state}|{int4 viewdate}|{str storage}|{str source}|{str other}|{int2 filestate}
                     */
                    //file already exists: read 'watched' status
                    string[] arrResult = receivedData.Split('\n');
                    if (arrResult.Length >= 2)
                    {
                        string[] arrStatus = arrResult[1].Split('|');
                        // We expect 0 for a MyListID
                        int.TryParse(arrStatus[0], out int myListID);

                        AniDBFile_State state = (AniDBFile_State) int.Parse(arrStatus[6]);

                        int viewdate = int.Parse(arrStatus[7]);
                        bool watched = viewdate > 0;

                        DateTime? watchedDate = null;
                        if (watched)
                        {
                            DateTime utcDate = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                            utcDate = utcDate.AddSeconds(viewdate);

                            watchedDate = utcDate.ToLocalTime();
                        }

                        return new AniDBUDP_Response<AniDBMyList_ResponseAddFile>
                        {
                            Code = code,
                            Response = new AniDBMyList_ResponseAddFile
                            {
                                MyListID = myListID,
                                State = state,
                                IsWatched = watched,
                                WatchedDate = watchedDate
                            }
                        };
                    }
                    break;
                }
            }
            throw new UnexpectedAniDBResponseException(code, receivedData);
        }
    }
}
