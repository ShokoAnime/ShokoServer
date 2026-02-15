using Newtonsoft.Json;
using Shoko.Server.Plex.Models;
using Shoko.Server.Plex.Models.Collection;
using Shoko.Server.Plex.Models.Libraries;

using MediaContainer = Shoko.Server.Plex.Models.Collection.MediaContainer;

namespace Shoko.Server.Plex.Libraries;

internal class SVR_Directory : Directory
{
    public SVR_Directory(PlexHelper helper)
    {
        Helper = helper;
    }

    private PlexHelper Helper { get; }

    public PlexLibrary[] GetShows()
    {
        var (_, json) = Helper.RequestFromPlexAsync($"/library/sections/{Key}/all").ConfigureAwait(false)
            .GetAwaiter().GetResult();
        return JsonConvert
            .DeserializeObject<MediaContainer<MediaContainer>>(json, Helper.SerializerSettings)
            .Container.Metadata;
    }
}
