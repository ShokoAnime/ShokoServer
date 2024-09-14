using System.IO;
using System.Linq;
using Shoko.Models.Plex.TVShow;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.Plex.TVShow;

internal class SVR_Episode : Episode
{
    private PlexHelper Helper { get; set; }

    public SVR_Episode(PlexHelper helper)
    {
        Helper = helper;
    }

    public SVR_AnimeEpisode AnimeEpisode
    {
        get
        {
            var separator = Helper.ServerCache.Platform.ToLower() switch
            {
                "linux" => '/',
                "windows" => '\\',
                "osx" => '/',
                "macos" => '/',
                "darwin" => '/',
                "android" => '/',
                
                _ => Path.DirectorySeparatorChar,
            };
            
            var filename = Media[0].Part[0].File.Split(separator).LastOrDefault();
            
            return filename is null ? null : RepoFactory.AnimeEpisode.GetByFilename(filename);

        }
    }

    public void Unscrobble()
    {
        Helper.RequestFromPlexAsync($"/:/unscrobble?identifier=com.plexapp.plugins.library&key={RatingKey}")
            .GetAwaiter().GetResult();
    }

    public void Scrobble()
    {
        Helper.RequestFromPlexAsync($"/:/scrobble?identifier=com.plexapp.plugins.library&key={RatingKey}")
            .GetAwaiter().GetResult();
    }
}
