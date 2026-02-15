using Shoko.Abstractions.Enums;

namespace Shoko.Abstractions.Metadata;

/// <summary>
/// A crew role.
/// </summary>
public interface ICrew : IMetadata<string>
{
    /// <summary>
    /// Creator ID.
    /// </summary>
    int CreatorID { get; }

    /// <summary>
    /// Parent entity ID.
    /// </summary>
    int ParentID { get; }

    /// <summary>
    /// Name of the crew role, in English.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Programmatic type of the crew role.
    /// </summary>
    CrewRoleType RoleType { get; }

    /// <summary>
    /// Parent metadata entity.
    /// </summary>
    IMetadata<int>? Parent { get; }

    /// <summary>
    /// Creator. Can be null if the metadata is
    /// currently not locally available.
    /// </summary>
    ICreator? Creator { get; }
}

/// <summary>
/// A crew role for a parent entity.
/// </summary>
/// <typeparam name="TMetadata">Metadata type.</typeparam>
public interface ICrew<TMetadata> : ICrew where TMetadata : IMetadata<int>
{
    /// <summary>
    /// Parent metadata entity.
    /// </summary>
    TMetadata? ParentOfType { get; }
}
