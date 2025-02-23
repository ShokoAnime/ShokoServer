using System.ComponentModel.DataAnnotations;
using Shoko.Plugin.Abstractions.Release;

#nullable enable
namespace Shoko.Server.API.v3.Models.Release;

public class ReleaseGroup : IReleaseGroup
{
    /// <summary>
    /// AniDB release group ID (69)
    /// </summary>
    [Required]
    public string ID { get; init; }

    /// <summary>
    /// The Release Group's Name (Unlimited Translation Works)
    /// </summary>
    [Required]
    public string Name { get; init; }

    /// <summary>
    /// The Release Group's Name (UTW)
    /// </summary>
    [Required]
    public string ShortName { get; init; }

    /// <summary>
    /// Source. Anidb, User, etc.
    /// </summary>
    [Required]
    public string Source { get; init; }

    public ReleaseGroup()
    {
        ID = string.Empty;
        Name = string.Empty;
        ShortName = string.Empty;
        Source = string.Empty;
    }

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
