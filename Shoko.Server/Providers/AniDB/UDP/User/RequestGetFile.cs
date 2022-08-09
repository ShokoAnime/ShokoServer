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
    public class RequestGetFile : UDPRequest<ResponseMyListFile>
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

        protected override UDPResponse<ResponseMyListFile> ParseResponse(ILogger logger, UDPResponse<string> response)
        {
            var code = response.Code;
            var receivedData = response.Response;
            switch (code)
            {
                case UDPReturnCode.NO_SUCH_ENTRY:
                    return new UDPResponse<ResponseMyListFile>
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
                    var arrStatus = receivedData.Split('|');
                    var hasMyListID = int.TryParse(arrStatus[0], out var myListID);
                    if (!hasMyListID) throw new UnexpectedUDPResponseException
                    {
                        Message = "MyListID was not provided. Use AniDBMyList_RequestAddEpisode for generic files.",
                        Response = receivedData,
                        ReturnCode = code
                    };

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
            }
            throw new UnexpectedUDPResponseException(code, receivedData);
        }
    }
}
