using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Shoko;

namespace Shoko.Abstractions.Actions;

/// <summary>
///   Base class for episode-scoped executable actions. Derive from this
///   (rather than implementing <see cref="IExecutableAction"/> directly) to
///   register an <see cref="ActionScope.Episode"/> action; the framework
///   populates <see cref="Episode"/> before <see cref="Execute"/> runs.
/// </summary>
public abstract class EpisodeAction : IExecutableAction, IScopedAction
{
    /// <summary>
    ///   The scope of the action.
    /// </summary>
    public ActionScope Scope => ActionScope.Episode;

    /// <summary>
    ///   The episode the action operates on. Non-null by the time
    ///   <see cref="Execute"/> runs — populated by the framework via
    ///   <see cref="IScopedAction.SetContext"/>.
    /// </summary>
    protected IShokoEpisode Episode { get; private set; } = null!;

    void IScopedAction.SetContext(object context)
        => Episode = (IShokoEpisode)context;

    /// <inheritdoc cref="IExecutableAction.Name"/>
    public abstract string Name { get; }

    /// <inheritdoc cref="IExecutableAction.Permission"/>
    public abstract ActionPermission Permission { get; }

    /// <inheritdoc cref="IExecutableAction.Execute"/>
    public abstract Task Execute(CancellationToken token = default);
}
