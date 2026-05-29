using System;

#nullable enable
namespace Shoko.Server.API.v3.Models.Plugin;

public class PluginCompatibilityInfo
{
    /// <summary>
    ///   Gets the current plugin abstraction ABI in use.
    /// </summary>
    public required Version AbstractionVersion { get; set; }

    /// <summary>
    ///   Gets the current runtime identifier for the platform in use.
    /// </summary>
    public required string RuntimeIdentifier { get; set; }
}
