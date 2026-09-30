using System.Collections.Generic;
using Shoko.Abstractions.Metadata;

namespace Shoko.Server.Settings;

/// <summary>
/// What has been decided about one image contributor.
/// </summary>
public class MetadataImageContributorSettings
{
    /// <summary>
    /// The kinds an admin turned the contributor off for, by source. Every
    /// other pair it can add images for is on, so a pair it gains later
    /// starts on.
    /// </summary>
    public Dictionary<MetadataSource, HashSet<MetadataEntityType>> Disabled { get; set; } = [];
}
