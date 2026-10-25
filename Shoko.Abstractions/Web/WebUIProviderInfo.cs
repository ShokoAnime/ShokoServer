using System;
using System.Collections.Generic;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Abstractions.Web.Enums;

namespace Shoko.Abstractions.Web;

/// <summary>
///   Contains information about a <see cref="IWebUIProvider"/>.
/// </summary>
public class WebUIProviderInfo
{
    /// <summary>
    ///   The unique ID of the provider.
    /// </summary>
    public required Guid ID { get; init; }

    /// <summary>
    ///   The version of the web UI provider.
    /// </summary>
    public required Version Version { get; init; }

    /// <summary>
    ///   The display name of the web UI.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///   Describes what the web UI is for.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    ///   The normalized path the web UI is served from, such as <c>/webui</c>,
    ///   or <c>/</c> for the root. Follows the provider's
    ///   <see cref="IWebUIProvider.MountPath"/>; a path it may not have is
    ///   refused, and the last allowed one is kept.
    /// </summary>
    public string MountPath { get; internal set; } = "/";

    /// <summary>
    ///   Templates for the web UI's pages, relative to the
    ///   <see cref="MountPath"/>.
    /// </summary>
    public IReadOnlyDictionary<WebUILinkType, string> Links { get => Provider.Links; }

    /// <summary>
    ///   The installed release of the web UI's files, or <c>null</c> if no
    ///   usable release is installed.
    /// </summary>
    public WebUIReleaseInformation? CurrentRelease { get => Provider.CurrentRelease; }

    /// <summary>
    ///   Indicates the web UI is the one served, or kept up to date while
    ///   serving is off: the <see cref="Services.IWebUIService.ActiveProvider"/>.
    /// </summary>
    public bool IsActive { get; internal set; }

    /// <summary>
    ///   Indicates the admin picked the web UI. It may not be the active one
    ///   when its plugin failed. When none is picked, one is picked
    ///   automatically.
    /// </summary>
    public bool IsSelected { get; internal set; }

    /// <summary>
    ///   The <see cref="IWebUIProvider"/> that this info is for.
    /// </summary>
    public required IWebUIProvider Provider { get; init; }

    /// <summary>
    ///   Information about the configuration that the web UI provider
    ///   uses.
    /// </summary>
    public required ConfigurationInfo? ConfigurationInfo { get; init; }

    /// <summary>
    ///   Information about the plugin that the web UI provider belongs to.
    /// </summary>
    public required LocalPluginInfo PluginInfo { get; init; }
}
