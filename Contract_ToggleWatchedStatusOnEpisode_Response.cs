using Shoko.Models.Client;
using Shoko.Models.Server;

namespace Shoko.Models
{
    public class Contract_ToggleWatchedStatusOnEpisode_Response
    {
        public string ErrorMessage { get; set; }
        public Client.CL_AnimeEpisode_User AnimeEpisode { get; set; }
    }
}