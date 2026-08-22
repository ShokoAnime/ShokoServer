using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Shoko;

namespace Shoko.Abstractions.Actions;

/// <summary>
///   Base class for group-scoped executable actions. Derive from this (rather
///   than implementing <see cref="IExecutableAction"/> directly) to register a
///   <see cref="ActionScope.Group"/> action; the framework populates
///   <see cref="Group"/> before <see cref="Execute"/> runs.
/// </summary>
public abstract class GroupAction : IExecutableAction, IScopedAction
{
    /// <summary>
    ///   The scope of the action.
    /// </summary>
    public ActionScope Scope => ActionScope.Group;

    /// <summary>
    ///   The group the action operates on. Non-null by the time
    ///   <see cref="Execute"/> runs — populated by the framework via
    ///   <see cref="IScopedAction.SetContext"/>.
    /// </summary>
    protected IShokoGroup Group { get; private set; } = null!;

    void IScopedAction.SetContext(object context)
        => Group = (IShokoGroup)context;

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
