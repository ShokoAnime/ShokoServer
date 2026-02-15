using Shoko.Abstractions.Enums;

namespace Shoko.Abstractions.Metadata.Stub;

/// <summary>
///   A stub implementation of the <see cref="ITitle" /> interface.
/// </summary>
public class TitleStub : TextStub, ITitle
{
    /// <inheritdoc />
    public TitleType Type { get; set; } = TitleType.None;

    /// <inheritdoc />
    public bool Equals(ITitle? other)
        => ITitle.Equals(this, other);
}
