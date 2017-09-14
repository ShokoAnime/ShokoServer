using Nancy.Rest.Annotations.Atributes;
using Nancy.Rest.Annotations.Enums;
using Shoko.Models.PlexAndKodi;

namespace Shoko.Models.Interfaces
{
    [RestBasePath("/api/Plex")]
    public interface IShokoServerPlex
    {
        [Rest("Image/Support/{name}", Verbs.Get)]
        System.IO.Stream GetSupportImage(string name);

        [Rest("Filters/{userId}", Verbs.Get)]
        MediaContainer GetFilters(string userId);

        [Rest("Metadata/{userId}/{type}/{id}/{historyinfo?}/{filterid?}", Verbs.Get)]
        MediaContainer GetMetadata(string userId, int type, string id, string historyinfo, int? filterid);

        [Rest("User", Verbs.Get)]
        PlexContract_Users GetUsers();

        [Rest("Search/{userId}/{limit}/{query}", Verbs.Get)]
        MediaContainer Search(string userId, int limit, string query);

        [Rest("Serie/Watch/{userId}/{epid}/{status}", Verbs.Get)]
        Response ToggleWatchedStatusOnEpisode(string userId, int epid, bool status);

        [Rest("Vote/{userId}/{id}/{votevalue}/{votetype}", Verbs.Get)]
        Response Vote(string userId, int id, float votevalue, int votetype);

    }
}
