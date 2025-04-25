using System;
using Shoko.Plugin.Abstractions;

namespace Shoko.Plugin.RelocationPlus;

/// <summary>
/// Plugin responsible for relocating video extra files near the video files.
/// </summary>
public class Plugin : IPlugin
{
    /// <inheritdoc/>
    public Guid ID { get; private set; } = typeof(Plugin).FullName!.ToUuidV5();

    /// <inheritdoc/>
    public string Name { get; private set; } = "Relocation+";
}
