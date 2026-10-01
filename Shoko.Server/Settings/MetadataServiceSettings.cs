using System;
using System.Collections.Generic;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.UI.Attributes;
using Shoko.Abstractions.UI.Enums;
using Shoko.Server.Services;

namespace Shoko.Server.Settings;

/// <summary>
/// Settings for the <see cref="MetadataService"/>.
/// <br/>
/// These are separate from the <see cref="ServerSettings"/> to prevent
/// clients from modifying them through the settings endpoint.
/// </summary>
public class MetadataServiceSettings : INewtonsoftJsonConfiguration, IHiddenConfiguration
{
    /// <summary>
    /// What has been decided about each source. A source with no entry is
    /// taken by the first provider claiming it on registration, the core's
    /// own before any plugin's.
    /// </summary>
    [Visibility(DisplayVisibility.ReadOnly)]
    public Dictionary<MetadataSource, MetadataSourceSettings> Sources { get; set; } = [];

    /// <summary>
    /// What has been decided about each image contributor, by its ID. A
    /// contributor with no entry is on for everything it can add images for.
    /// </summary>
    [Visibility(DisplayVisibility.ReadOnly)]
    public Dictionary<Guid, MetadataImageContributorSettings> ImageContributors { get; set; } = [];
}
