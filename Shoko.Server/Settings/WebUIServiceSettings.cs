using System;
using System.Collections.Generic;
using System.ComponentModel;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Web;
using Shoko.Server.Services;

namespace Shoko.Server.Settings;

/// <summary>
/// Settings for the <see cref="WebUIService"/>.
/// <br/>
/// These are separate from the <see cref="ServerSettings"/> to prevent
/// web UIs from modifying them through the settings endpoint.
/// </summary>
public class WebUIServiceSettings : INewtonsoftJsonConfiguration
{
    /// <summary>
    /// Serve the web UI. Turning it off runs the server "headless"; the web
    /// UI is still kept up to date.
    /// </summary>
    [EnvironmentVariable("SHOKO_WEBUI_ENABLED")]
    [DefaultValue(true)]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// The ID of the web UI to serve by default, or <c>null</c> to pick one
    /// automatically.
    /// </summary>
    [EnvironmentVariable("SHOKO_WEBUI_PROVIDER")]
    public Guid? SelectedProviderID { get; set; }

    /// <summary>
    /// The web UIs served for some hosts instead of the default one, which
    /// serves every other host.
    /// </summary>
    public List<WebUIHostOverride> HostOverrides { get; set; } = [];
}
