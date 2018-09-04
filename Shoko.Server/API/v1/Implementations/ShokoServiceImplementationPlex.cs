using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shoko.Models.Interfaces;
using Shoko.Models.Plex.Connections;
using Shoko.Models.PlexAndKodi;
using Shoko.Server.PlexAndKodi;
using Shoko.Server.PlexAndKodi.Plex;
using MediaContainer = Shoko.Models.PlexAndKodi.MediaContainer;

namespace Shoko.Server.API.v1.Implementations
{
    [ApiController]
    [Route("/api/Plex")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class ShokoServiceImplementationPlex : IShokoServerPlex, IHttpContextAccessor
    {
        public HttpContext HttpContext { get; set; }

        CommonImplementation _impl = new CommonImplementation();

        [HttpGet("Image/Support/{name}")]
        public System.IO.Stream GetSupportImage(string name)
        {
            return _impl.GetSupportImage(name);
        }

        [HttpGet("Filters/{userId}")]
        public MediaContainer GetFilters(string userId)
        {
            return _impl.GetFilters(new PlexProvider {Nancy = HttpContext}, userId);
        }

        [HttpGet("Metadata/{userId}/{type}/{id}/{historyinfo}/{filterid?}")]
        public MediaContainer GetMetadata(string userId, int type, string id, string historyinfo, int? filterid)
        {
            return _impl.GetMetadata(new PlexProvider {Nancy = HttpContext}, userId, type, id, historyinfo,
                false, filterid);
        }

        [HttpGet("User")]
        public PlexContract_Users GetUsers()
        {
            return _impl.GetUsers(new PlexProvider {Nancy = HttpContext});
        }

        [HttpGet("Search/{userId}/{limit}/{query}")]
        public MediaContainer Search(string userId, int limit, string query)
        {
            return _impl.Search(new PlexProvider {Nancy = HttpContext}, userId, limit, query, false);
        }

        [HttpGet("Serie/Watch/{userId}/{epid}/{status}")]
        public Response ToggleWatchedStatusOnEpisode(string userId, int epid, bool status)
        {
            return _impl.ToggleWatchedStatusOnEpisode(new PlexProvider {Nancy = HttpContext}, userId, epid,
                status);
        }

        [HttpGet("Vote/{userId}/{id}/{votevalue}/{votetype}")]
        public Response Vote(string userId, int id, float votevalue, int votetype)
        {
            return _impl.VoteAnime(new PlexProvider {Nancy = HttpContext}, userId, id, votevalue,
                votetype);
        }

        [HttpGet("Linking/Devices/Current/{userId}")]
        public MediaDevice CurrentDevice(int userId) => _impl.CurrentDevice(userId);

        [HttpPost("Linking/Directories/{userId}")]
        public void UseDirectories(int userId, List<Shoko.Models.Plex.Libraries.Directory> directories) =>
            _impl.UseDirectories(userId, directories);

        [HttpGet("Linking/Directories/{userId}")]
        public Shoko.Models.Plex.Libraries.Directory[] Directories(int userId) => _impl.Directories(userId);

        [HttpPost("Linking/Servers/{userId}")]
        public void UseDevice(int userId, MediaDevice server) => _impl.UseDevice(userId, server);

        [HttpGet("Linking/Devices/{userId}")]
        public MediaDevice[] AvailableDevices(int userId) => _impl.AvailableDevices(userId);


        [HttpGet("Metadata/{userId}/{type}/{id}")]
        public MediaContainer GetMetadataWithoutHistory(string userId, int type, string id) => GetMetadata(userId, type, id, null, null);
    }
}