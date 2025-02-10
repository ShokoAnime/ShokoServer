using Shoko.Plugin.Abstractions.Release;

#nullable enable
namespace Shoko.Server.API.v3.Models.Common;

public class ReleaseGroup
{
    /// <summary>
    /// AniDB release group ID (69)
    /// /// </summary>
    public string ID { get; set; }

    /// <summary>
    /// The Release Group's Name (Unlimited Translation Works)
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The Release Group's Name (UTW)
    /// </summary>
    public string? ShortName { get; set; }

    /// <summary>
    /// Source. Anidb, User, etc.
    /// </summary>
    /// <value></value>
    public string Source { get; set; }

    public ReleaseGroup()
    {
        ID = "0";
        Source = "Unknown";
    }

    public ReleaseGroup(IReleaseGroup group)
    {
        ID = group.ID;
        Name = group.Name;
        ShortName = group.ShortName;
        Source = group.ProviderID;
    }
}
