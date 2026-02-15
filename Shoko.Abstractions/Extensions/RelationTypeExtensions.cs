using Shoko.Abstractions.Enums;

namespace Shoko.Abstractions.Extensions;

/// <summary>
/// Extensions for the <see cref="RelationType"/> enum.
/// </summary>
public static class RelationTypeExtensions
{
    /// <summary>
    /// Reverse the relation.
    /// </summary>
    /// <param name="type">The relation to reverse.</param>
    /// <returns>The reversed relation.</returns>
    public static RelationType Reverse(this RelationType type)
    {
        return type switch
        {
            RelationType.Prequel => RelationType.Sequel,
            RelationType.Sequel => RelationType.Prequel,
            RelationType.MainStory => RelationType.SideStory,
            RelationType.SideStory => RelationType.MainStory,
            RelationType.FullStory => RelationType.Summary,
            RelationType.Summary => RelationType.FullStory,
            _ => type
        };
    }
}
