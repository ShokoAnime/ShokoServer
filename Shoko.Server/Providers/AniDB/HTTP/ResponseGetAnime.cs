using System;
using System.Collections.Generic;
using Shoko.Server.Providers.AniDB.Http.GetAnime;

namespace Shoko.Server.Providers.AniDB.Http
{
    public class ResponseGetAnime
    {
        public ResponseAnime Anime { get; set; }
        public List<ResponseEpisode> Episodes { get; set; }
    }
}
