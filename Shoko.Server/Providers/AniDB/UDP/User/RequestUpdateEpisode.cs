using System;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Void = Shoko.Server.Providers.AniDB.UDP.Generic.Void;

namespace Shoko.Server.Providers.AniDB.UDP.User
{
    /// <summary>
    /// Update a file in the MyList
    /// </summary>
    public class RequestUpdateEpisode : UDPBaseRequest<Void>
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

                command += "&edit=1";

                return command;
            }
        }

        public int AnimeID { get; set; }

        public int EpisodeNumber { get; set; }

        public GetFile_State State { get; set; }

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
            throw new UnexpectedUDPResponseException(code, receivedData);
        }
    }
}
