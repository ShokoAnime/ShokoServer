namespace Shoko.Plugin.Abstractions.Release;

/// <summary>
/// Contains information about a <see cref="IReleaseInfoProvider"/>.
/// </summary>
public class ReleaseInfoProviderInfo
{
    /// <summary>
    /// The <see cref="IReleaseInfoProvider"/> that this info is for.
    /// </summary>
    public required IReleaseInfoProvider Provider { get; init; }

    /// <summary>
    /// Whether or not the provider is enabled for automatic usage.
    /// </summary>
    public required bool Enabled { get; set; }

    /// <summary>
    /// The priority of the provider during automatic usage.
    /// </summary>
    public required int Priority { get; set; }
}

