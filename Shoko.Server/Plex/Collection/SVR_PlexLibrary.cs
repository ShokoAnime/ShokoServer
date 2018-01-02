using Newtonsoft.Json;
using Shoko.Commons.Plex;
using Shoko.Commons.Plex.Collection;

namespace Shoko.Server.Plex.Collection
{
    internal class SVR_PlexLibrary : PlexLibrary
    {
        public SVR_PlexLibrary(PlexHelper helper)
        {
            Helper = helper;
        }

        private PlexHelper Helper { get; }

        public Commons.Plex.TVShow.Episode[] GetEpisodes()
        {
            var (_, data) = Helper.RequestFromPlexAsync($"/library/metadata/{RatingKey}/allLeaves").GetAwaiter()
                .GetResult();
            return JsonConvert
                .DeserializeObject<MediaContainer<Commons.Plex.TVShow.MediaContainer>>(data, Helper.SerializerSettings)
                .Container.Metadata;
        }
    }
}