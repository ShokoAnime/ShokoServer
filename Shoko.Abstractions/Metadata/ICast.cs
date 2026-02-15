using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Metadata.Containers;

namespace Shoko.Abstractions.Metadata;

/// <summary>
/// A cast role.
/// </summary>
public interface ICast : IMetadata<string>, IWithPortraitImage
{
    /// <summary>
    /// Creator ID, if the cast role has a known creator.
    /// </summary>
    int? CreatorID { get; }

    /// <summary>
    /// Character ID, if the cast role has a character shared with one or more
    /// other cast roles.
    /// </summary>
    int? CharacterID { get; }

    /// <summary>
    /// Parent entity ID.
    /// </summary>
    int ParentID { get; }

    /// <summary>
    /// Casted role name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Casted role name in the original language of the media, if available
    /// from
    /// </summary>
    string? OriginalName { get; }

    /// <summary>
    /// Role description, if available from the provider.
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// Role type.
    /// </summary>
    CastRoleType RoleType { get; }

    /// <summary>
    /// Parent metadata entity.
    /// </summary>
    IMetadata<int>? Parent { get; }

    /// <summary>
    /// Character, if the cast role has a character shared with one or more
    /// other cast roles.
    /// </summary>
    ICharacter? Character { get; }

    /// <summary>
    /// Creator. Can be null if the cast role has no known creator or if the
    /// metadata is currently not locally available.
    /// </summary>
    ICreator? Creator { get; }
}

/// <summary>
/// A cast role for a parent entity.
/// </summary>
/// <typeparam name="TMetadata">Metadata type.</typeparam>
public interface ICast<TMetadata> : ICast where TMetadata : IMetadata<int>
{
    /// <summary>
    /// Parent metadata entity.
    /// </summary>
    TMetadata? ParentOfType { get; }
}
