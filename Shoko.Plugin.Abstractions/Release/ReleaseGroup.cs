
namespace Shoko.Plugin.Abstractions.Release;

/// <summary>
/// Release group.
/// </summary>
public class ReleaseGroup : IReleaseGroup
{
    /// <inheritdoc />
    public string ID { get; set; } = string.Empty;

    /// <inheritdoc />
    public string ProviderID { get; set; } = string.Empty;

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
        ProviderID = group.ProviderID;
        Name = group.Name;
        ShortName = group.ShortName;
    }
}
