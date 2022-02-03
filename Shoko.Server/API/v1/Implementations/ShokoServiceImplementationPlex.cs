using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shoko.Models.Interfaces;
using Shoko.Models.Plex.Connections;
using Shoko.Models.PlexAndKodi;
using Shoko.Server.PlexAndKodi;
using Shoko.Server.PlexAndKodi.Plex;
using Directory = Shoko.Models.Plex.Libraries.Directory;
using MediaContainer = Shoko.Models.PlexAndKodi.MediaContainer;
using Stream = System.IO.Stream;

namespace Shoko.Server.API.v1.Implementations
{
    [ApiController, Route("/api/Plex"), ApiVersion("1.0", Deprecated = true)]
    public class ShokoServiceImplementationPlex : IShokoServerPlex, IHttpContextAccessor
    {
        public HttpContext HttpContext { get; set; }

        CommonImplementation _impl = new CommonImplementation();

        [HttpGet("Image/Support/{name}")]
        public Stream GetSupportImage(string name)
        {
            return _impl.GetSupportImage(name);
        }

        [HttpGet("Filters/{userId}")]
        public MediaContainer GetFilters(string userId)
        {
            return _impl.GetFilters(new PlexProvider {HttpContext = HttpContext}, userId);
        }

        [HttpGet("Metadata/{userId}/{type}/{id}/{historyinfo}/{filterid?}")]
        public MediaContainer GetMetadata(string userId, int type, string id, string historyinfo, int? filterid)
        {
            return _impl.GetMetadata(new PlexProvider {HttpContext = HttpContext}, userId, type, id, historyinfo,
                false, filterid);
        }

        [HttpGet("User")]
        public PlexContract_Users GetUsers()
        {
            return _impl.GetUsers(new PlexProvider {HttpContext = HttpContext});
        }

        [HttpGet("Search/{userId}/{limit}/{query}")]
        public MediaContainer Search(string userId, int limit, string query)
        {
            return _impl.Search(new PlexProvider {HttpContext = HttpContext}, userId, limit, query, false);
        }

        [HttpGet("Serie/Watch/{userId}/{epid}/{status}")]
        public Response ToggleWatchedStatusOnEpisode(string userId, int epid, bool status)
        {
            return _impl.ToggleWatchedStatusOnEpisode(new PlexProvider {HttpContext = HttpContext}, userId, epid,
                status);
        }

        [HttpGet("Vote/{userId}/{id}/{votevalue}/{votetype}")]
        public Response Vote(string userId, int id, float votevalue, int votetype)
        {
            return _impl.VoteAnime(new PlexProvider {HttpContext = HttpContext}, userId, id, votevalue,
                votetype);
        }

        [HttpGet("Linking/Devices/Current/{userId}")]
        public MediaDevice CurrentDevice(int userId) => _impl.CurrentDevice(userId);

        [HttpPost("Linking/Directories/{userId}")]
        public void UseDirectories(int userId, List<Directory> directories) =>
            _impl.UseDirectories(userId, directories);

        [HttpGet("Linking/Directories/{userId}")]
        public Directory[] Directories(int userId) => _impl.Directories(userId);

        [HttpPost("Linking/Servers/{userId}")]
        public void UseDevice(int userId, MediaDevice server) => _impl.UseDevice(userId, server);

        [HttpGet("Linking/Devices/{userId}")]
        public MediaDevice[] AvailableDevices(int userId) => _impl.AvailableDevices(userId) ?? new MediaDevice[0];


        [HttpGet("Metadata/{userId}/{type}/{id}")]
        public MediaContainer GetMetadataWithoutHistory(string userId, int type, string id) => GetMetadata(userId, type, id, null, null);
    }
}