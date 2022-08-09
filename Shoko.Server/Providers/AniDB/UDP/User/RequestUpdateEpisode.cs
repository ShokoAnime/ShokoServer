using System;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Void = Shoko.Server.Providers.AniDB.UDP.Generic.Void;

namespace Shoko.Server.Providers.AniDB.UDP.User
{
    /// <summary>
    /// Update a file in the MyList
    /// </summary>
    public class RequestUpdateEpisode : UDPRequest<Void>
    {
        protected override string BaseCommand
        {
            get
            {
                var type = "";
                if (EpisodeType != EpisodeType.Episode)
                    type = EpisodeType.ToString()[..1];
                var command = $"MYLISTADD aid={AnimeID}&epno={type+EpisodeNumber}&generic=1&state={(int)State}";
                if (IsWatched)
                {
                    var date = WatchedDate ?? DateTime.Now;
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

        public int AnimeID { get; set; }

        public int EpisodeNumber { get; set; }

        public EpisodeType EpisodeType { get; set; } = EpisodeType.Episode;

        public MyList_State State { get; set; }

        public bool IsWatched { get; set; }
        public DateTime? WatchedDate { get; set; }

        protected override UDPResponse<Void> ParseResponse(ILogger logger, UDPResponse<string> response)
        {
            var code = response.Code;
            var receivedData = response.Response;
            switch (code)
            {
                case UDPReturnCode.MYLIST_ENTRY_EDITED:
                case UDPReturnCode.NO_SUCH_MYLIST_ENTRY:
                    return new UDPResponse<Void> {Code = code};
            }
            throw new UnexpectedUDPResponseException(code, receivedData);
        }
    }
}
