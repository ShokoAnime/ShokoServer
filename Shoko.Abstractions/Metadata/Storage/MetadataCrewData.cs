using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Storage;

/// <summary>
///   One crew credit on an entry. It is known by its creator and its job. A
///   creator not stored yet is kept as a stub.
/// </summary>
public sealed record MetadataCrewData
{
    /// <summary>
    ///   The creator credited.
    /// </summary>
    public required MetadataGuid CreatorID { get; init; }

    /// <summary>
    ///   The creator's name, the main title of the stub the core makes when
    ///   the creator is not stored yet.
    /// </summary>
    public string? CreatorName { get; init; }

    /// <summary>
    ///   The job, as the source writes it.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///   The kind of job, where the source's wording maps onto one.
    /// </summary>
    public CrewRoleType RoleType { get; init; }

    /// <summary>
    ///   The language the job was done in, as a language code, when it
    ///   matters, such as for a translation.
    /// </summary>
    public string? LanguageCode { get; init; }
}
