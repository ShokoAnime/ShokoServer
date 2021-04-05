using System;
using Shoko.Models.Enums;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Void = Shoko.Server.Providers.AniDB.UDP.Generic.Void;

namespace Shoko.Server.Providers.AniDB.UDP.User
{
    /// <summary>
    /// Update a file in the MyList
    /// </summary>
    public class RequestUpdateFile : UDPBaseRequest<Generic.Void>
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

        protected override UDPBaseResponse<Void> ParseResponse(UDPReturnCode code, string receivedData)
        {
            switch (code)
            {
                case UDPReturnCode.MYLIST_ENTRY_EDITED:
                case UDPReturnCode.NO_SUCH_MYLIST_ENTRY:
                    return new UDPBaseResponse<Void> {Code = code};
            }
            throw new UnexpectedAniDBResponseException(code, receivedData);
        }

    }
}
