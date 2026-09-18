using System;
using Shoko.Abstractions.Config.Enums;

namespace Shoko.Abstractions.Config.Attributes;

/// <summary>
///   Attribute used for marking a method as a reactive configuration action.
/// /// </summary>
/// <param name="actionType">
///   The type of action to perform on the configuration when this method is
///   called.
/// </param>
[AttributeUsage(AttributeTargets.Method)]
public class ConfigurationActionAttribute(ConfigurationActionType actionType) : Attribute()
{
    /// <summary>
    ///   The type of action to perform on the configuration when this method is
    ///   called.
    /// </summary>
    public ConfigurationActionType ActionType { get; set; } = actionType;

    /// <summary>
    ///   The reactive events this action handles. Leave it unset to handle
    ///   every event.
    /// </summary>
    /// <remarks>
    ///   A request carries one event, while a handler is interested in as many
    ///   as it likes, so this is the plural side of the same enum. A handler
    ///   that names the raised event is preferred over one that named none.
    ///   Only <see cref="ConfigurationActionType.LiveEdit"/> is raised by an
    ///   event, so this says nothing on any other hook.
    /// </remarks>
    public ReactiveEventType[]? Events { get; set; }

    /// <summary>
    ///   The members this action watches. Leave it unset to watch everything in
    ///   the class it is declared on, and everything below it that has no
    ///   handler of its own.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     Naming the members is what lets a client tell whether an edit is
    ///     worth sending at all: it reaches the element as
    ///     <c>ReactsToLiveEdit</c>, and an edit to anything else is never sent.
    ///   </para>
    ///   <para>
    ///     A name may descend into a nested class, spelled
    ///     <c>$"{nameof(Nested)}.{nameof(NestedConfiguration.Member)}"</c>,
    ///     which stays a constant and survives a rename. It cannot point
    ///     through a list or a dictionary, which carries no index to say which
    ///     entry it meant.
    ///   </para>
    ///   <para>
    ///     Only <see cref="ConfigurationActionType.LiveEdit"/> is raised by an
    ///     edit, so this says nothing on any other hook.
    ///   </para>
    /// </remarks>
    public string[]? ReactiveMembers { get; set; }
}
