using System;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;

namespace Shoko.Server.Providers.AniDB.UDP.User
{
    /// <summary>
    /// Add a file to MyList. If it doesn't exist, it will return the MyListID for future updates.
    /// If it exists, it will return the current status on AniDB.
    /// </summary>
    public class RequestGetFile : UDPBaseRequest<ResponseMyListFile>
    {
        // These are dependent on context
        protected override string BaseCommand
        {
            get
            {
                return $"MYLIST size={Size}&ed2k={Hash}";
            }
        }

        public string Hash { get; set; }

        public long Size { get; set; }

        public GetFile_State State { get; set; }

        public bool IsWatched { get; set; }

        public DateTime? WatchedDate { get; set; }

        protected override UDPBaseResponse<ResponseMyListFile> ParseResponse(ILogger logger, UDPBaseResponse<string> response)
        {
            var code = response.Code;
            var receivedData = response.Response;
            switch (code)
            {
                case UDPReturnCode.NO_SUCH_ENTRY:
                    return new UDPBaseResponse<ResponseMyListFile>
                    {
                        Code = code,
                        Response = null,
                    };
                case UDPReturnCode.MYLIST:
                {
                    /* Response Format
                     * {int4 lid}|{int4 fid}|{int4 eid}|{int4 aid}|{int4 gid}|{int4 date}|{int2 state}|{int4 viewdate}|{str storage}|{str source}|{str other}|{int2 filestate}
                     */
                    //file already exists: read 'watched' status
                    string[] arrResult = receivedData.Split('\n');
                    if (arrResult.Length >= 2)
                    {
                        string[] arrStatus = arrResult[1].Split('|');
                        bool hasMyListID = int.TryParse(arrStatus[0], out int myListID);
                        if (!hasMyListID) throw new UnexpectedUDPResponseException
                        {
                            Message = "MyListID was not provided. Use AniDBMyList_RequestAddEpisode for generic files.",
                            Response = receivedData,
                            ReturnCode = code
                        };

                        GetFile_State state = (GetFile_State) int.Parse(arrStatus[6]);

                        int viewdate = int.Parse(arrStatus[7]);
                        int updatedate = int.Parse(arrStatus[5]);
                        bool watched = viewdate > 0;

                        DateTime? watchedDate = null;
                        DateTime updatedAt = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                            .AddSeconds(updatedate)
                            .ToLocalTime();
                        if (watched)
                            watchedDate = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                                .AddSeconds(viewdate)
                                .ToLocalTime();

                        return new UDPBaseResponse<ResponseMyListFile>
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
