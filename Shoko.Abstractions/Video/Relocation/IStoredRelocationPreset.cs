using System;

namespace Shoko.Abstractions.Video.Relocation;

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
