using System;

namespace Shoko.Abstractions.Video.Relocation;

/// <summary>
///   Identifies a relocation provider and carries the packed configuration for
///   that provider. Used as a lightweight preset reference for ad-hoc
///   relocation operations (e.g., <see cref="AutoRelocateRequest"/> or
///   <c>ProcessPipe</c> calls), without requiring database persistence.
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

/// <summary>
///   A relocation preset that is persisted in the database, with an identity,
///   display name, and default-flag. Extends <see cref="IRelocationPreset"/>
///   with lifecycle support for create, update, and delete operations via
///   <see cref="Services.IVideoRelocationService"/>.
/// </summary>
public interface IStoredRelocationPreset : IRelocationPreset
{
    /// <summary>
    /// The ID of the preset.
    /// </summary>
    Guid ID { get; }

    /// <summary>
    /// The friendly name of the preset, for display.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Indicates if this preset is the default preset.
    /// </summary>
    bool IsDefault { get; }
}
