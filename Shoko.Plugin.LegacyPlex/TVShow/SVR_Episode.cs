using System;
using System.IO;
using System.Linq;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Plugin.LegacyPlex.Models.TVShow;

namespace Shoko.Plugin.LegacyPlex.TVShow;

internal class SVR_Episode : Episode
{
    private PlexHelper Helper { get; set; }

    public SVR_Episode(PlexHelper helper)
    {
        Helper = helper;
    }

    public IShokoEpisode? AnimeEpisode
    {
        get
        {
            var separator = Helper.ServerCache.Platform!.ToLower() switch
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

            var file = Helper.VideoService
                .GetAllVideoFiles()
                .FirstOrDefault(location => location.Path.EndsWith(filenameWithParent, StringComparison.OrdinalIgnoreCase));

            return file?.Video.Episodes.FirstOrDefault();
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
