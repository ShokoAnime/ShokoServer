using System;
using System.Collections.Generic;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;
using Shoko.Server.Services;

namespace Shoko.Server.Settings;

/// <summary>
/// Settings for the <see cref="VideoHashingService"/>.
/// <br/>
/// These are separate from the <see cref="ServerSettings"/> to prevent
/// clients from modifying them through the settings endpoint.
/// </summary>
public class VideoHashingServiceSettings : INewtonsoftJsonConfiguration, IHiddenConfiguration
{
    /// <summary>
    /// Whether or not to use parallel mode for the service.
    /// </summary>
    public bool ParallelMode { get; set; } = false;

    /// <summary>
    /// A dictionary containing the enabled hashes and their provider's ID.
    /// </summary>
    [Visibility(DisplayVisibility.ReadOnly)]
    public Dictionary<string, Guid> EnabledHashes { get; set; } = [];
}
