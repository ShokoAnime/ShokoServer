using System;

namespace Shoko.Abstractions.Video.Relocation;

/// <summary>
///   Identifies a relocation provider and carries the packed configuration for
///   that provider. Used as a lightweight preset reference for ad-hoc
///   relocation operations (e.g., <see cref="AutoRelocateRequest"/>), without
///   requiring database persistence.
/// </summary>
public interface IRelocationPreset
{
    /// <summary>
    /// The provider ID for this preset.
    /// </summary>
    Guid ProviderID { get; }

    /// <summary>
    ///   The packed configuration for this preset, if the
    ///   <see cref="IRelocationProvider"/> for the <see cref="ProviderID"/>
    ///   supports configuration.
    /// </summary>
    byte[]? Configuration { get; }
}
