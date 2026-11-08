using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

using AbstractThirdPartyNoticeSection = Shoko.Abstractions.Plugin.Models.ThirdPartyNoticeSection;

namespace Shoko.Server.API.v3.Models.Plugin;

/// <summary>
/// The third-party notices of one loaded plugin, the core included.
/// </summary>
public class ThirdPartyNoticeSection(AbstractThirdPartyNoticeSection section)
{
    /// <summary>
    /// The plugin's ID.
    /// </summary>
    [Required]
    public Guid PluginID { get; init; } = section.PluginID;

    /// <summary>
    /// The plugin's name.
    /// </summary>
    [Required]
    public string PluginName { get; init; } = section.PluginName;

    /// <summary>
    /// The plugin's version.
    /// </summary>
    [Required]
    public string PluginVersion { get; init; } = section.PluginVersion.ToString();

    /// <summary>
    /// Whether this is the core's section.
    /// </summary>
    [Required]
    public bool IsCore { get; init; } = section.IsCore;

    /// <summary>
    /// The plugin's own licence, heading its section, or <see langword="null"/> when its notices do not give one.
    /// </summary>
    public ThirdPartyNotice? Project { get; init; } = section.Project is { } project ? new(project) : null;

    /// <summary>
    /// The third-party packages and assets it ships, sorted by ID.
    /// </summary>
    [Required]
    public IReadOnlyList<ThirdPartyNotice> Packages { get; init; } = [.. section.Packages.Select(notice => new ThirdPartyNotice(notice))];
}
