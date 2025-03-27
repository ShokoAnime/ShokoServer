using System;
using Shoko.Plugin.Abstractions;

namespace Shoko.Plugin.ReleaseExporter;

/// <summary>
/// Plugin responsible for importing releases to and exporting releases from the file system near the video files.
/// </summary>
public class Plugin : IPlugin
{
    /// <inheritdoc/>
    public Guid ID { get; private set; } = typeof(Plugin).FullName!.ToUuidV5();

    /// <inheritdoc/>
    public string Name { get; private set; } = "Release Importer/Exporter";

    /// <inheritdoc/>
    public void Load() { }
}
