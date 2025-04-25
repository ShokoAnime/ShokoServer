using Shoko.Plugin.Abstractions;

namespace Shoko.Plugin.OfflineImporter;

/// <summary>
/// Plugin responsible for importing releases based on file names.
/// </summary>
public class Plugin : IPlugin
{
    /// <inheritdoc/>
    public string Name { get; private init; } = "Offline Importer";

    /// <inheritdoc/>
    public string Description { get; private init; } = """
        Plugin responsible for importing releases based on file names.
    """;
}
