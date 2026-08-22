using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Video;

namespace Shoko.Abstractions.Actions;

/// <summary>
///   Base class for video-scoped executable actions. Derive from this
///   (rather than implementing <see cref="IExecutableAction"/> directly) to
///   register an <see cref="ActionScope.Video"/> action; the framework
///   populates <see cref="Video"/> before <see cref="Execute"/> runs.
/// </summary>
public abstract class VideoAction : IExecutableAction, IScopedAction
{
    /// <summary>
    ///   The scope of the action.
    /// </summary>
    public ActionScope Scope => ActionScope.Video;

    /// <summary>
    ///   The video the action operates on. Non-null by the time
    ///   <see cref="Execute"/> runs — populated by the framework via
    ///   <see cref="IScopedAction.SetContext"/>.
    /// </summary>
    protected IVideo Video { get; private set; } = null!;

    void IScopedAction.SetContext(object context)
        => Video = (IVideo)context;

    /// <inheritdoc cref="IExecutableAction.Name"/>
    public abstract string Name { get; }

    /// <inheritdoc cref="IExecutableAction.Description"/>
    public virtual string? Description => null;

    /// <inheritdoc cref="IExecutableAction.Category"/>
    public virtual ActionCategory Category => ActionCategory.Miscellaneous;

    /// <inheritdoc cref="IExecutableAction.Permission"/>
    public abstract ActionPermission Permission { get; }

    /// <inheritdoc cref="IExecutableAction.RequiresConfirmation"/>
    public virtual bool RequiresConfirmation => false;

    /// <inheritdoc cref="IExecutableAction.ConfirmationMessage"/>
    public virtual string? ConfirmationMessage => null;

    /// <inheritdoc cref="IExecutableAction.Validate"/>
    public virtual Task<ActionValidationResult?> Validate(CancellationToken token = default)
        => Task.FromResult<ActionValidationResult?>(null);

    /// <inheritdoc cref="IExecutableAction.Execute"/>
    public abstract Task Execute(CancellationToken token = default);
}
