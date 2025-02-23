using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Shoko.Commons;
using Shoko.Models.Server;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v2.Models.core;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.API.v2.Modules;

[ApiController]
[Route("/api/version")]
[ApiVersion("2.0")]
[InitFriendly]
[DatabaseBlockedExempt]
public class Version : BaseController
{
    /// <summary>
    /// Return current version of ShokoServer and several modules
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public List<ComponentVersion> GetVersion()
    {
        var list = new List<ComponentVersion>();

        var version = new ComponentVersion { version = Utils.GetApplicationVersion(), name = "server" };
        list.Add(version);

        var extraVersionDict = Utils.GetApplicationExtraVersion();
        if (extraVersionDict.TryGetValue("commit", out var versionExtra))
        {
            version = new ComponentVersion { version = versionExtra, name = "servercommit" };
            list.Add(version);
        }

        version = new ComponentVersion
        {
            version = Utils.GetApplicationVersion(), name = "commons"
        };
        list.Add(version);

        version = new ComponentVersion
        {
            version = Utils.GetApplicationVersion(), name = "models"
        };
        list.Add(version);

        /*version = new ComponentVersion
        {
            version = Assembly.GetAssembly(typeof(INancyModule)).GetName().Version.ToString(),
            name = "Nancy"
        };
        list.Add(version);*/

        var dllpath = Assembly.GetEntryAssembly().Location;
        dllpath = Path.GetDirectoryName(dllpath);
        dllpath = Path.Combine(dllpath, "x86");
        dllpath = Path.Combine(dllpath, "MediaInfo.dll");

        if (System.IO.File.Exists(dllpath))
        {
            version = new ComponentVersion
            {
                version = FileVersionInfo.GetVersionInfo(dllpath).FileVersion, name = "MediaInfo"
            };
            list.Add(version);
        }
        else
        {
            dllpath = Assembly.GetEntryAssembly().Location;
            dllpath = Path.GetDirectoryName(dllpath);
            dllpath = Path.Combine(dllpath, "x64");
            dllpath = Path.Combine(dllpath, "MediaInfo.dll");
            if (System.IO.File.Exists(dllpath))
            {
                version = new ComponentVersion
                {
                    version = FileVersionInfo.GetVersionInfo(dllpath).FileVersion, name = "MediaInfo"
                };
                list.Add(version);
            }
            else
            {
                version = new ComponentVersion { version = @"DLL not found, using internal", name = "MediaInfo" };
                list.Add(version);
            }
        }

        var webUIFileInfo = new FileInfo(Path.Combine(Utils.ApplicationPath, "webui/index.ver"));
        if (webUIFileInfo.Exists)
        {
            var webui_version = System.IO.File.ReadAllText(webUIFileInfo.FullName);
            var versions = webui_version.Split('>');
            if (versions.Length == 2)
            {
                version = new ComponentVersion { name = "webui/" + versions[0], version = versions[1] };
                list.Add(version);
            }
        }

        return list;
    }

    public Version(ISettingsProvider settingsProvider) : base(settingsProvider)
    {
    }
}
