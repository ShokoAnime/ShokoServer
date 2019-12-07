using System;
using Shoko.Models.Enums;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Requests;
using Shoko.Server.Providers.AniDB.UDP.Responses;

namespace Shoko.Server.Providers.AniDB.UDP.MyList.Requests
{
    /// <summary>
    /// Update a file in the MyList
    /// </summary>
    public class AniDBMyList_RequestUpdateFile : AniDBUDP_BaseRequest<string>
    {
        protected override string BaseCommand
        {
            get
            {
                string command = $"MYLISTADD lid={MyListID}&state={State}";
                if (IsWatched)
                {
                    DateTime date = WatchedDate ?? DateTime.Now;
                    command += $"&viewed=1&viewdate={Commons.Utils.AniDB.GetAniDBDateAsSeconds(date)}";
                }
                else
                {
                    command += "&viewed=0";
                }

                command += "&edit=1";

                return command;
            }
        }

        public int MyListID { get; set; }

        public AniDBFile_State State { get; set; }

        public bool IsWatched { get; set; }
        public DateTime? WatchedDate { get; set; }

        protected override AniDBUDP_Response<string> ParseResponse(AniDBUDPReturnCode code, string receivedData)
        {
            switch (code)
            {
                case AniDBUDPReturnCode.MYLIST_ENTRY_EDITED:
                case AniDBUDPReturnCode.NO_SUCH_MYLIST_ENTRY:
                    return new AniDBUDP_Response<string> {Code = code};
            }
            throw new UnexpectedAniDBResponseException(code, receivedData);
        }

    }
}
