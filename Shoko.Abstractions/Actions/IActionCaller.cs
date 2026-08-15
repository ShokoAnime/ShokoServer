using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Actions;

/// <summary>
///   Opt-in marker interface for actions that want to know which user invoked
///   them.
/// </summary>
/// <remarks>
///   <para>
///     Distinct from <see cref="ActionScope"/>'s entity context. Any action,
///     of any scope, may implement this.
///   </para>
///   <para>
///     Unlike <see cref="IScopedAction"/>, this interface is not internal:
///     there is no equivalent bypass concern, since a wrong or missing caller
///     just means the action does not know who invoked it, not a security
///     boundary.
///   </para>
/// </remarks>
public interface IActionCaller
{
    /// <summary>
    ///   Sets the calling user. Called by the framework only, before
    ///   <see cref="IExecutableAction.Validate"/> and
    ///   <see cref="IExecutableAction.Execute"/>.
    /// </summary>
    /// <param name="caller">
    ///   The user that invoked the action.
    /// </param>
    void SetCaller(IUser caller);
}
