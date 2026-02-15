using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Shoko.Abstractions.Web.Attributes;
using Shoko.Server.API.v2.Models.core;
using Shoko.Server.API.v3.Controllers;
using Shoko.Server.Settings;

#nullable enable
namespace Shoko.Server.API.v2.Modules;

[ApiController]
[Route("/api/version")]
[ApiVersion("2.0")]
[InitFriendly]
[DatabaseBlockedExempt]
public class VersionController(ISettingsProvider settingsProvider, InitController init) : BaseController(settingsProvider)
{
    /// <summary>
    /// Return current version of ShokoServer and several modules
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public List<ComponentVersion> GetVersion()
        => init.GetVersion() is not { } versionSet ? [] : [
            new() { name = "server", version = versionSet.Server.Version },
            new() { name = "commons", version = versionSet.Commons?.Version },
            new() { name = "models", version = versionSet.Models?.Version },
            new() { name = "MediaInfo", version = versionSet.MediaInfo?.Version },
            new() { name = "webui", version = versionSet.WebUI?.Version },
        ];
}
