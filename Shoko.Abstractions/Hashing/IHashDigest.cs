
using System;

namespace Shoko.Abstractions.Hashing;

/// <summary>
/// Hash digest interface.
/// </summary>
public interface IHashDigest : IEquatable<IHashDigest>, IComparable<IHashDigest>
{
    /// <summary>
    /// Hash type.
    /// </summary>
    string Type { get; }

    /// <summary>
    /// Hash digest value.
    /// </summary>
    string Value { get; }

    /// <summary>
    /// Hash specific metadata, if any.
    /// </summary>
    string? Metadata { get; }
}
