
using System;
using Newtonsoft.Json;
using Shoko.Abstractions.Plugin.Models;

#nullable enable
namespace Shoko.Server.API.v3.Models.Plugin;

/// <summary>
///   A page exposed by a plugin.
/// </summary>
public class PluginPage
{
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
    /// /// </summary>
    public bool CanEmbed { get; init; }

    /// <summary>
    ///   The plugin this page belongs to. Will be <c>null</c> if the page is
    ///   exposed as part of the plugin info.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public PluginInfo? PluginInfo { get; init; }

    public PluginPage(LocalPluginPage page, string baseUrl, bool includePluginInfo = false)
    {
        Name = page.Name;
        if (Uri.IsWellFormedUriString(page.Url, UriKind.Absolute))
            Url = page.Url;
        else
            Url = page.Url.StartsWith("/")
                ? baseUrl + page.Url[1..]
                : baseUrl + page.Url;
        CanEmbed = page.CanEmbed;
        if (includePluginInfo)
            PluginInfo = new(page.PluginInfo);
    }
}
