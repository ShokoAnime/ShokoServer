using Newtonsoft.Json;
using Shoko.Server.Plex.Models;
using Shoko.Server.Plex.Models.Collection;
using Shoko.Server.Plex.Models.TVShow;
using MediaContainer = Shoko.Server.Plex.Models.TVShow.MediaContainer;

namespace Shoko.Server.Plex.Collection;

internal class SVR_PlexLibrary : PlexLibrary
{
    public SVR_PlexLibrary(PlexHelper helper)
    {
        Helper = helper;
    }

    private PlexHelper Helper { get; }

    public Episode[] GetEpisodes()
    {
        var (_, data) = Helper.RequestFromPlexAsync($"/library/metadata/{RatingKey}/allLeaves").GetAwaiter()
            .GetResult();
        return JsonConvert
            .DeserializeObject<MediaContainer<MediaContainer>>(data, Helper.SerializerSettings)
            .Container.Metadata;
    }
}
