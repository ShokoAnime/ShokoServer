using System;

namespace Shoko.Plugin.Abstractions.Relocation;

/// <summary>
///   Generic implementation of <see cref="IRelocationPipe"/> which can be used
///   with <see cref="AutoRelocateRequest"/>s if not using a stored relocation
///   pipe.
/// </summary>
/// <param name="providerID">
///   The provider ID.
/// </param>
/// <param name="configuration">
///   The configuration, in packed form, if any.
/// </param>
public class RelocationPipe(Guid providerID, byte[]? configuration) : IRelocationPipe
{
    /// <inheritdoc/>
    public Guid ProviderID => providerID;

    /// <inheritdoc/>
    public byte[]? Configuration => configuration;
}
