using System;
using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Config;
using Shoko.Server.Services;

namespace Shoko.Server.Settings;

/// <summary>
/// Settings for the <see cref="VideoReleaseService"/>.
/// <br/>
/// These are separate from the <see cref="ServerSettings"/> to prevent
/// clients from modifying them through the settings endpoint.
/// </summary>
public class VideoReleaseServiceSettings : INewtonsoftJsonConfiguration, IHiddenConfiguration
{
    /// <summary>
    /// Whether or not to use parallel mode for the service.
    /// </summary>
    public bool ParallelMode { get; set; } = false;

    /// <summary>
    /// A dictionary containing the enabled state of each provider by id.
    /// </summary>
    public Dictionary<Guid, bool> Enabled { get; set; } = [];

    /// <summary>
    /// A list of provider ids in order of priority.
    /// </summary>
    public List<Guid> Priority { get; set; } = [];
}
