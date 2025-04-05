
namespace Shoko.Plugin.Abstractions.Release;

/// <summary>
/// Release group.
/// </summary>
public class ReleaseGroup : IReleaseGroup
{
    /// <inheritdoc />
    public string ID { get; set; } = string.Empty;

    /// <inheritdoc />
    public string Name { get; set; } = string.Empty;

    /// <inheritdoc />
    public string ShortName { get; set; } = string.Empty;

    /// <inheritdoc />
    public string Source { get; set; } = string.Empty;

    /// <inheritdoc />
    public ReleaseGroup() { }

    /// <inheritdoc />
    public ReleaseGroup(IReleaseGroup group)
    {
        ID = group.ID;
        Name = group.Name;
        ShortName = group.ShortName;
        Source = group.Source;
    }

    /// <inheritdoc/>
    public bool Equals(IReleaseGroup? other)
        => other is not null &&
           string.Equals(ID, other.ID) &&
           string.Equals(Source, other.Source);
}
