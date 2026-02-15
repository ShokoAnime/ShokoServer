using System;
using Shoko.Abstractions.Plugin;

namespace Shoko.Abstractions.Config.Attributes;

/// <summary>
///   Attribute used for allowing plugins to specify a custom save location for
///   their configuration.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class StorageLocationAttribute() : Attribute()
{
    /// <summary>
    ///   Determines if the configuration should only be stored in memory, which
    ///   will not persist it's data to the file system and will be reset when
    ///   the application restarts.
    /// </summary>
    public bool InMemoryOnly { get; set; }

    /// <summary>
    ///   The name of the file to use in the plugin's configuration folder
    ///   inside <see cref="IApplicationPaths.ConfigurationsPath"/> for
    ///   storing the configuration.
    /// </summary>
    /// <value>
    ///   The file name.
    /// </value>
    public string? FileName { get; set; }

    /// <summary>
    ///   Gets the relative path relative to
    ///   <see cref="IApplicationPaths.DataPath"/> for storing the
    ///   configuration.
    /// </summary>
    /// <value>
    ///   The relative path.
    /// </value>
    public string? RelativePath { get; set; }
}
