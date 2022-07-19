using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Void = Shoko.Server.Providers.AniDB.UDP.Generic.Void;

namespace Shoko.Server.Providers.AniDB.UDP.User
{
    public class RequestRemoveEpisode : UDPRequest<Void>
    {
        protected override string BaseCommand
        {
            get
            {
                var type = "";
                if (EpisodeType != EpisodeType.Episode)
                    type = EpisodeType.ToString()[..1];
                return $"MYLISTDEL aid={AnimeID}&epno={type+EpisodeNumber}";
            }
        }

        public int AnimeID { get; set; }

        public int EpisodeNumber { get; set; }
        public EpisodeType EpisodeType { get; set; } = EpisodeType.Episode;

        protected override UDPResponse<Void> ParseResponse(ILogger logger, UDPResponse<string> response)
        {
            var code = response.Code;
            var receivedData = response.Response;
            switch (code)
            {
                case UDPReturnCode.MYLIST_ENTRY_DELETED:
                case UDPReturnCode.NO_SUCH_MYLIST_ENTRY:
                    return new UDPResponse<Void> { Code = code };
            }
            throw new UnexpectedUDPResponseException(code, receivedData);
        }
    }
}
