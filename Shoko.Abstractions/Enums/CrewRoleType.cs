
namespace Shoko.Abstractions.Enums;

/// <summary>
/// Role types.
/// </summary>
public enum CrewRoleType : byte
{
    /// <summary>
    /// This can be anything involved in writing the show or movie.
    /// </summary>
    None = 0,

    /// <summary>
    /// The main producer(s) for the show or movie.
    /// </summary>
    Producer,

    /// <summary>
    /// Direction.
    /// </summary>
    Director,

    /// <summary>
    /// Series Composition.
    /// </summary>
    SeriesComposer,

    /// <summary>
    /// Character Design.
    /// </summary>
    CharacterDesign,

    /// <summary>
    /// Music composer.
    /// </summary>
    Music,

    /// <summary>
    /// Responsible for the creation of the source work the show or movie is derived from.
    /// </summary>
    SourceWork,

    /// <summary>
    /// Actor.
    /// </summary>
    Actor,
}
