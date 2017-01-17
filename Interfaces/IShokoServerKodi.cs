using Nancy.Rest.Annotations.Atributes;
using Nancy.Rest.Annotations.Enums;
using Shoko.Models.PlexAndKodi;

namespace Shoko.Models.Interfaces
{
    [RestBasePath("/api/Kodi")]
    public interface IShokoServerKodi
    {
        [Rest("Image/Support/{name}",Verbs.Get)]
        System.IO.Stream GetSupportImage(string name);

        [Rest("Filters/{userId}", Verbs.Get)]
        MediaContainer GetFilters(string userId);

        [Rest("Metadata/{userId}/{type}/{id}/{filterid?}", Verbs.Get)]
        MediaContainer GetMetadata(string userId, int type, string id, int? filterid);

        [Rest("User", Verbs.Get)]
        PlexContract_Users GetUsers();

        [Rest("Version", Verbs.Get)]
        Response Version();

        [Rest("Search/{userId}/{limit}/{query}", Verbs.Get)]
        MediaContainer Search(string userId, int limit, string query);

        [Rest("SearchTag/{userId}/{limit}/{query}", Verbs.Get)]
        MediaContainer SearchTag(string userId, int limit, string query);

        [Rest("Group/Watch/{userId}/{groupid}/{status}", Verbs.Get)]
        Response ToggleWatchedStatusOnGroup(string userId, int groupid, bool status);

        [Rest("Serie/Watch/{userId}/{serieid}/{status}", Verbs.Get)]
        Response ToggleWatchedStatusOnSeries(string userId, int serieid, bool status);

        [Rest("Serie/Watch/{userId}/{epid}/{status}", Verbs.Get)]
        Response ToggleWatchedStatusOnEpisode(string userId, int epid, bool status);

        [Rest("Vote/{userId}/{id}/{votevalue}/{votetype}", Verbs.Get)]
        Response Vote(string userId, int id, float votevalue, int votetype);

        [Rest("Trakt/Scrobble/{animeid}/{type}/{progress}/{status}", Verbs.Get)]
        Response TraktScrobble(string animeid, int type, float progress, int status);

        [Rest("Video/Rescan/{vlid}", Verbs.Get)]
        Response Rescan(int vlid);

        [Rest("Video/Rehash/{vlid}", Verbs.Get)]
        Response Rehash(int vlid);


    }
}
