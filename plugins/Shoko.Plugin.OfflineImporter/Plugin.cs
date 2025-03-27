using System;
using Shoko.Plugin.Abstractions;

namespace Shoko.Plugin.OfflineImporter;

/// <summary>
/// Plugin responsible for importing releases based on file names.
/// </summary>
public class Plugin : IPlugin
{
    /// <inheritdoc/>
    public Guid ID { get; private set; } = typeof(Plugin).FullName!.ToUuidV5();

    /// <inheritdoc/>
    public string Name { get; private set; } = "Offline Importer";

    /// <inheritdoc/>
    public void Load() { }
}
