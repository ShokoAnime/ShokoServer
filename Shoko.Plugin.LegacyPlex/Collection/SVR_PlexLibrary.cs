using Newtonsoft.Json;
using Shoko.Plugin.LegacyPlex.Models;
using Shoko.Plugin.LegacyPlex.Models.Collection;
using Shoko.Plugin.LegacyPlex.Models.TVShow;

using MediaContainer = Shoko.Plugin.LegacyPlex.Models.TVShow.MediaContainer;

namespace Shoko.Plugin.LegacyPlex.Collection;

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
            .DeserializeObject<MediaContainer<MediaContainer>>(data, Helper.SerializerSettings)!
            .Container.Metadata;
    }
}
