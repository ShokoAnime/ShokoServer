using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Release;

/// <summary>
/// Release group.
/// </summary>
public class ReleaseGroup : IReleaseGroup
{
    /// <inheritdoc />
    public int ID { get; set; }

    /// <inheritdoc />
    public DataSourceEnum Source { get; set; }

    /// <inheritdoc />
    public string? Name { get; set; }

    /// <inheritdoc />
    public string? ShortName { get; set; }

    /// <inheritdoc />
    public ReleaseGroup() { }

    /// <inheritdoc />
    public ReleaseGroup(IReleaseGroup group)
    {
        ID = group.ID;
        Source = group.Source;
        Name = group.Name;
        ShortName = group.ShortName;
    }
}
