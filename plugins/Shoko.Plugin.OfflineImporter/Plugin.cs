using System;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Utilities;

namespace Shoko.Plugin.OfflineImporter;

/// <summary>
/// Plugin responsible for importing releases based on file names.
/// </summary>
public class Plugin : IPlugin
{
    /// <inheritdoc/>
    public Guid ID { get => UuidUtility.GetV5(GetType().FullName!); }

    /// <inheritdoc/>
    public string Name { get; private init; } = "Offline Importer";

    /// <inheritdoc/>
    public string Description { get; private init; } = """
        Plugin responsible for importing releases based on file names.
    """;
}
