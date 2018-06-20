using System.Collections.Generic;
using Nancy.Rest.Annotations.Atributes;
using Nancy.Rest.Annotations.Enums;
using Shoko.Models.Plex.Connections;
using Shoko.Models.PlexAndKodi;
using MediaContainer = Shoko.Models.PlexAndKodi.MediaContainer;

namespace Shoko.Models.Interfaces
{
    [RestBasePath("/api/Plex")]
    public interface IShokoServerPlex
    {
        [Rest("Image/Support/{name}", Verbs.Get)]
        System.IO.Stream GetSupportImage(string name);

        [Rest("Filters/{userId}", Verbs.Get)]
        MediaContainer GetFilters(string userId);

        [Rest("Metadata/{userId}/{type}/{id}/{historyinfo}/{filterid?}", Verbs.Get)]
        MediaContainer GetMetadata(string userId, int type, string id, string historyinfo, int? filterid);

        [Rest("Metadata/{userId}/{type}/{id}", Verbs.Get)]
        MediaContainer GetMetadataWithoutHistory(string userId, int type, string id);

        [Rest("User", Verbs.Get)]
        PlexContract_Users GetUsers();

        [Rest("Search/{userId}/{limit}/{query}", Verbs.Get)]
        MediaContainer Search(string userId, int limit, string query);

        [Rest("Serie/Watch/{userId}/{epid}/{status}", Verbs.Get)]
        Response ToggleWatchedStatusOnEpisode(string userId, int epid, bool status);

        [Rest("Vote/{userId}/{id}/{votevalue}/{votetype}", Verbs.Get)]
        Response Vote(string userId, int id, float votevalue, int votetype);

        #region Plex Linking

        [Rest("Linking/Devices/Current/{userId}", Verbs.Get, TimeOutSeconds = 600)]
        MediaDevice CurrentDevice(int userId);

        [Rest("Linking/Directories/{userId}", Verbs.Post)]
        void UseDirectories(int userId, List<Plex.Libraries.Directory> directories);

        [Rest("Linking/Directories/{userId}", Verbs.Get, TimeOutSeconds = 600)]
        Plex.Libraries.Directory[] Directories(int userId);

        [Rest("Linking/Servers/{userId}", Verbs.Post)]
        void UseDevice(int userId, MediaDevice server);

        [Rest("Linking/Devices/{userId}", Verbs.Get, TimeOutSeconds = 600)]
        MediaDevice[] AvailableDevices(int userId);

        #endregion

    }
}
