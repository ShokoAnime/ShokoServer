using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Void = Shoko.Server.Providers.AniDB.UDP.Generic.Void;

namespace Shoko.Server.Providers.AniDB.UDP.User
{
    public class RequestRemoveEpisode : UDPBaseRequest<Void>
    {
        protected override string BaseCommand
        {
            get
            {
                return $"MYLISTDEL aid={AnimeID}&epno={EpisodeNumber}";
            }
        }

        public int AnimeID { get; set; }

        public int EpisodeNumber { get; set; }

        protected override UDPBaseResponse<Void> ParseResponse(ILogger logger, UDPBaseResponse<string> response)
        {
            var code = response.Code;
            var receivedData = response.Response;
            switch (code)
            {
                case UDPReturnCode.MYLIST_ENTRY_DELETED:
                case UDPReturnCode.NO_SUCH_MYLIST_ENTRY:
                    return new UDPBaseResponse<Void> { Code = code };
            }
            throw new UnexpectedUDPResponseException(code, receivedData);
        }
    }
}
