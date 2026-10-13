using Newtonsoft.Json;
using Shoko.Plugin.LegacyPlex.Models;
using Shoko.Plugin.LegacyPlex.Models.Collection;
using Shoko.Plugin.LegacyPlex.Models.Libraries;

using MediaContainer = Shoko.Plugin.LegacyPlex.Models.Collection.MediaContainer;

namespace Shoko.Plugin.LegacyPlex.Libraries;

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
            .DeserializeObject<MediaContainer<MediaContainer>>(json, Helper.SerializerSettings)!
            .Container.Metadata;
    }
}
