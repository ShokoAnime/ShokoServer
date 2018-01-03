using System.IO;
using Shoko.Models.Plex.TVShow;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.Plex.TVShow
{
    class SVR_Episode : Episode
    {
        private PlexHelper Helper { get; set; }

        public SVR_Episode(PlexHelper helper)
        {
            Helper = helper;
        }

        public SVR_AnimeEpisode AnimeEpisode =>
            RepoFactory.AnimeEpisode.GetByFilename(Path.GetFileName(Media[0].Part[0].File));

        public void Unscrobble()
        {
            Helper.RequestFromPlexAsync($"/:/unscrobble?identifier=com.plexapp.plugins.library&key={Key}")
                .GetAwaiter().GetResult();
        }

        public void Scrobble()
        {
            Helper.RequestFromPlexAsync($"/:/scrobble?identifier=com.plexapp.plugins.library&key={Key}")
                .GetAwaiter().GetResult();
        }
    }
}
