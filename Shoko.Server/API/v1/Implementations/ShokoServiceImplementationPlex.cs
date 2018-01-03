using Nancy.Rest.Module;
using Shoko.Models.Interfaces;
using Shoko.Models.Plex.Connections;
using Shoko.Models.PlexAndKodi;
using Shoko.Server.PlexAndKodi;
using Shoko.Server.PlexAndKodi.Plex;
using MediaContainer = Shoko.Models.PlexAndKodi.MediaContainer;

namespace Shoko.Server.API.v1.Implementations
{
    public class ShokoServiceImplementationPlex : IShokoServerPlex
    {
        CommonImplementation _impl = new CommonImplementation();

        public System.IO.Stream GetSupportImage(string name)
        {
            return _impl.GetSupportImage(name);
        }

        public MediaContainer GetFilters(string userId)
        {
            return _impl.GetFilters(new PlexProvider {Nancy = RestModule.CurrentModule}, userId);
        }

        public MediaContainer GetMetadata(string userId, int type, string id, string historyinfo, int? filterid)
        {
            return _impl.GetMetadata(new PlexProvider {Nancy = RestModule.CurrentModule}, userId, type, id, historyinfo,
                false, filterid);
        }

        public PlexContract_Users GetUsers()
        {
            return _impl.GetUsers(new PlexProvider {Nancy = RestModule.CurrentModule});
        }

        public MediaContainer Search(string userId, int limit, string query)
        {
            return _impl.Search(new PlexProvider {Nancy = RestModule.CurrentModule}, userId, limit, query, false);
        }

        public Response ToggleWatchedStatusOnEpisode(string userId, int epid, bool status)
        {
            return _impl.ToggleWatchedStatusOnEpisode(new PlexProvider {Nancy = RestModule.CurrentModule}, userId, epid,
                status);
        }

        public Response Vote(string userId, int id, float votevalue, int votetype)
        {
            return _impl.VoteAnime(new PlexProvider {Nancy = RestModule.CurrentModule}, userId, id, votevalue,
                votetype);
        }

        public void UseDirectories(int userId, Shoko.Models.Plex.Libraries.Directory[] directories) =>
            _impl.UseDirectories(userId, directories);

        public Shoko.Models.Plex.Libraries.Directory[] Directories(int userId) => _impl.Directories(userId);

        public void UseDevice(int userId, MediaDevice server) => _impl.UseDevice(userId, server);

        public MediaDevice[] AvailableDevices(int userId) => _impl.AvailableDevices(userId);

        public MediaContainer GetMetadataWithoutHistory(string userId, int type, string id) => GetMetadata(userId, type, id, null, null);
    }
}