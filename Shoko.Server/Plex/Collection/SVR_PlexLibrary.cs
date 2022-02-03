using Newtonsoft.Json;
using Shoko.Models.Plex;
using Shoko.Models.Plex.Collection;
using Shoko.Models.Plex.TVShow;
using MediaContainer = Shoko.Models.Plex.TVShow.MediaContainer;

namespace Shoko.Server.Plex.Collection
{
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
}