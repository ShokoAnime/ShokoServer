
using System;

namespace Shoko.Plugin.Abstractions.Hashing;

/// <summary>
/// Basic hash digest implementation.
/// </summary>
public class HashDigest : IHashDigest
{
    /// <inheritdoc />
    public required string Type { get; set; }

    /// <inheritdoc />
    public required string Value { get; set; }

    /// <inheritdoc />
    public string? Metadata { get; set; }

    /// <inheritdoc/>
    public int CompareTo(IHashDigest? other)
    {
        if (other is null)
            return 1;

        var result = string.Compare(Type, other.Type, StringComparison.InvariantCulture);
        if (result != 0)
            return result;

        result = string.Compare(Value, other.Value, StringComparison.InvariantCulture);
        if (result != 0)
            return result;

        return string.Compare(Metadata, other.Metadata, StringComparison.InvariantCulture);
    }

    /// <inheritdoc/>
    public bool Equals(IHashDigest? other)
    {
        if (other is null)
            return false;

        return string.Equals(Type, other.Type, StringComparison.InvariantCulture) &&
            string.Equals(Value, other.Value, StringComparison.InvariantCulture) &&
            string.Equals(Metadata, other.Metadata, StringComparison.InvariantCulture);
    }
}
