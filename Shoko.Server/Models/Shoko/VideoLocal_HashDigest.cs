using System;
using Shoko.Abstractions.Hashing;

#nullable enable
namespace Shoko.Server.Models.Shoko;

public class VideoLocal_HashDigest : IHashDigest
{
    public int VideoLocal_HashDigestID { get; set; }

    public int VideoLocalID { get; set; }

    /// <inheritdoc />
    public string Type { get; set; } = string.Empty;

    /// <inheritdoc />
    public string Value { get; set; } = string.Empty;

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

    public override bool Equals(object? obj)
        => obj is not null && (
            obj is VideoLocal_HashDigest other0 ? Equals(other0) :
            obj is IHashDigest other1 && Equals(other1)
        );

    public bool Equals(VideoLocal_HashDigest? other) =>
        other is not null &&
        VideoLocalID == other.VideoLocalID &&
        string.Equals(Type, other.Type, StringComparison.InvariantCulture) &&
        string.Equals(Value, other.Value, StringComparison.InvariantCulture) &&
        string.Equals(Metadata, other.Metadata, StringComparison.InvariantCulture);

    public bool Equals(IHashDigest? other) =>
        other is not null &&
        string.Equals(Type, other.Type, StringComparison.InvariantCulture) &&
        string.Equals(Value, other.Value, StringComparison.InvariantCulture) &&
        string.Equals(Metadata, other.Metadata, StringComparison.InvariantCulture);

    public override int GetHashCode()
        => HashCode.Combine(VideoLocalID, Type, Value, Metadata);
}
