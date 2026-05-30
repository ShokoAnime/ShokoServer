using System;

namespace Shoko.Abstractions.Video.Relocation;

/// <summary>
///   Generic implementation of <see cref="IRelocationPreset"/> which can be
///   used with <see cref="AutoRelocateRequest"/>s if not using a stored
///   relocation preset.
/// </summary>
/// <param name="providerID">
///   The provider ID.
/// </param>
/// <param name="configuration">
///   The configuration, in packed form, if any.
/// </param>
public class RelocationPreset(Guid providerID, byte[]? configuration) : IRelocationPreset
{
    /// <inheritdoc/>
    public Guid ProviderID => providerID;

    /// <inheritdoc/>
    public byte[]? Configuration => configuration;
}
