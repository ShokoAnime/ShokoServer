using System;
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

            var filenameWithParent = Path.Join(Media[0].Part[0].File.Split(separator)[^2..]);

            var file = RepoFactory.VideoLocalPlace
                .GetAll()
                .FirstOrDefault(location => location.FullServerPath?.EndsWith(filenameWithParent, StringComparison.OrdinalIgnoreCase) ?? false);

            return file is null ? null : RepoFactory.AnimeEpisode.GetByHash(file.Hashes.ED2K).FirstOrDefault();

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
