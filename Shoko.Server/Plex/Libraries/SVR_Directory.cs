using Newtonsoft.Json;
using Shoko.Models.Plex;
using Shoko.Models.Plex.Libraries;
using Shoko.Models.Plex.Collection;
using MediaContainer = Shoko.Models.Plex.Collection.MediaContainer;

namespace Shoko.Server.Plex.Libraries
{
    class SVR_Directory : Directory
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
}