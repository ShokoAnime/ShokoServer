using System;
using Shoko.Abstractions.Metadata;

namespace Shoko.Server.Models.Metadata;

/// <summary>
///   A row of a creator, character, studio or network, which the core may
///   keep as a stub until the entry's source saves it.
/// </summary>
public interface IMetadataStubRow : IMetadata
{
    /// <summary>
    ///   The entry's name, or an empty string when a stub was given none.
    /// </summary>
    string Name { get; set; }

    /// <summary>
    ///   When the source last wrote the entry, or <c>null</c> for a stub.
    /// </summary>
    DateTime? LastUpdatedAt { get; }

    /// <summary>
    ///   Since when nothing has named the entry, or <c>null</c> while
    ///   something does.
    /// </summary>
    DateTime? LastOrphanedAt { get; }

    /// <summary>
    ///   When the core last asked the source to refresh the entry, found or
    ///   not, or <c>null</c> when it never did.
    /// </summary>
    DateTime? LastRefreshedAt { get; }

    /// <summary>
    ///   Whether the entry is a stub: a row the core made for a credit or a
    ///   link before the source wrote it, holding only the name it carried.
    /// </summary>
    bool IsStub { get; }
}
