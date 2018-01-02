using Newtonsoft.Json;
using Shoko.Commons.Plex;
using Shoko.Commons.Plex.Libraries;

namespace Shoko.Server.Plex.Libraries
{
    class SVR_Directory : Directory
    {
        public SVR_Directory(PlexHelper helper)
        {
            Helper = helper;
        }

        private PlexHelper Helper { get; }

        public Commons.Plex.Collection.PlexLibrary[] GetShows()
        {
            var (_, json) = Helper.RequestFromPlexAsync($"/library/sections/{Key}/all").ConfigureAwait(false)
                .GetAwaiter().GetResult();
            return JsonConvert
                .DeserializeObject<MediaContainer<Commons.Plex.Collection.MediaContainer>>(json, Helper.SerializerSettings)
                .Container.Metadata;
        }
    }
}