using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.v1.Models;
using Shoko.Server.Plex;
using Shoko.Server.Plex.Models.Connections;
using Shoko.Server.Plex.Models.Libraries;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v1.Implementations;

[ApiController]
[Route("/api/Plex")]
[ApiVersion("1.0", Deprecated = true)]
public class ShokoServiceImplementationPlex : IHttpContextAccessor
{
    public HttpContext HttpContext { get; set; }
    private readonly ISettingsProvider _settingsProvider;

    public ShokoServiceImplementationPlex(ISettingsProvider settingsProvider)
    {
        _settingsProvider = settingsProvider;
    }

    [HttpGet("User")]
    public CL_PlexContract_Users GetUsers()
    {
        var gfs = new CL_PlexContract_Users
        {
            Users = []
        };
        foreach (var us in RepoFactory.JMMUser.GetAll())
        {
            var p = new CL_PlexContract_User { id = us.JMMUserID.ToString(), name = us.Username };
            gfs.Users.Add(p);
        }

        return gfs;
    }

    [HttpGet("Linking/Devices/Current/{userId}")]
    public MediaDevice CurrentDevice(int userId)
    {
        return PlexHelper.GetForUser(RepoFactory.JMMUser.GetByID(userId)).ServerCache;
    }

    [HttpPost("Linking/Directories/{userId}")]
    public void UseDirectories(int userId, List<Directory> directories)
    {
        var settings = _settingsProvider.GetSettings();
        if (directories == null)
        {
            settings.Plex.Libraries = [];
            return;
        }

        settings.Plex.Libraries = directories.Select(s => s.Key).ToList();
        _settingsProvider.SaveSettings();
    }

    [HttpGet("Linking/Directories/{userId}")]
    public Directory[] Directories(int userId)
    {
        return PlexHelper.GetForUser(RepoFactory.JMMUser.GetByID(userId)).GetDirectories();
    }

    [HttpPost("Linking/Servers/{userId}")]
    public void UseDevice(int userId, MediaDevice server)
    {
        PlexHelper.GetForUser(RepoFactory.JMMUser.GetByID(userId)).UseServer(server);
    }

    [HttpGet("Linking/Devices/{userId}")]
    public MediaDevice[] AvailableDevices(int userId)
    {
        return PlexHelper.GetForUser(RepoFactory.JMMUser.GetByID(userId)).GetPlexServers().ToArray();
    }
}
