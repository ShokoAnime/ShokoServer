
using Shoko.Abstractions.Plugin.Enums;

namespace Shoko.Abstractions.Plugin.Models;

/// <summary>
///   Represents a page exposed by a plugin.
/// </summary>
public sealed class PluginPage
{
    /// <summary>
    ///   The name of the page.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    ///   The relative or absolute URL to where to redirect the user.
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    ///   Indicates that the page can be embedded within the Web UI. Set to
    ///   false to force the page to open in a new window.
    /// </summary>
    public bool CanEmbed { get; set; } = true;

    /// <summary>
    ///   Who the page is offered to. This only decides who is shown the link,
    ///   so the page and its routes still guard themselves. Defaults to
    ///   <see cref="PluginPageVisibility.Admin"/>.
    /// </summary>
    public PluginPageVisibility Visibility { get; set; } = PluginPageVisibility.Admin;
}
