namespace Shoko.Abstractions.Actions;

/// <summary>
///   Marks a scoped action. Implemented exclusively by the three scoped base
///   classes (<see cref="SeriesAction"/>, <see cref="GroupAction"/>,
///   <see cref="EpisodeAction"/>).
/// </summary>
/// <remarks>
///   This interface is <see langword="internal"/> to this assembly so a
///   plugin cannot implement it directly and bypass the three base classes.
///   Only the base classes in this assembly may implement it.
/// </remarks>
internal interface IScopedAction
{
    /// <summary>
    ///   Sets the entity context. Called by the framework only, before
    ///   <see cref="IExecutableAction.Validate"/> and
    ///   <see cref="IExecutableAction.Execute"/>.
    /// </summary>
    /// <param name="context">
    ///   The entity the action will operate on.
    /// </param>
    void SetContext(object context);
}
