
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Plugin.Enums;
using Shoko.Abstractions.Plugin.Models;

namespace Shoko.Server.API.v3.Models.Plugin;

/// <summary>
///   A page exposed by a plugin.
/// </summary>
public class PluginPage
{
    /// <summary>
    ///   The ID of the page.
    /// </summary>
    public Guid ID { get; init; }

    /// <summary>
    ///   The name of the page.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    ///   The relative or absolute URL to where to redirect the user.
    /// </summary>
    public string Url { get; init; }

    /// <summary>
    ///   Indicates that the page can be embedded within the Web UI. Set to
    ///   false to force the page to open in a new window.
    /// </summary>
    public bool CanEmbed { get; init; }

    /// <summary>
    ///   Who the page is offered to.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public PluginPageVisibility Visibility { get; init; }

    /// <summary>
    ///   The ID of the plugin the page belongs to.
    /// </summary>
    public Guid PluginID { get; init; }

    /// <summary>
    ///   The plugin this page belongs to. Only included for administrators,
    ///   and left out when the page is exposed as part of the plugin info.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public PluginInfo? PluginInfo { get; init; }

    public PluginPage(LocalPluginPage page, bool includePluginInfo = false)
    {
        ID = page.ID;
        Name = page.Name;
        if (Uri.IsWellFormedUriString(page.Url, UriKind.Absolute))
            Url = page.Url;
        else
            Url = page.Url.StartsWith("/")
                ? page.Url
                : "/" + page.Url;
        CanEmbed = page.CanEmbed;
        Visibility = page.Visibility;
        PluginID = page.PluginInfo.ID;
        if (includePluginInfo)
            PluginInfo = new(page.PluginInfo);
    }
}
