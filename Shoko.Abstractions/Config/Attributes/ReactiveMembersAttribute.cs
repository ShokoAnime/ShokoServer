using System;

namespace Shoko.Abstractions.Config.Attributes;

/// <summary>
/// Narrow a live-edit handler to the members it actually watches.
/// </summary>
/// <remarks>
/// <para>
/// A handler is declared on a class, so without this nothing says which of the
/// class's members it cares about, and a client has to post the document for
/// every edit anywhere in it. Naming the members turns that into a flag per
/// element, and an edit to anything else is never sent.
/// </para>
/// <para>
/// Leaving it off keeps the old meaning: the handler watches everything in the
/// class it is declared on, and everything below it that has no handler of its
/// own.
/// </para>
/// <para>
/// Only <see cref="Enums.ConfigurationActionType.LiveEdit"/> is raised by an
/// edit, so this belongs on a live-edit handler and nowhere else.
/// </para>
/// </remarks>
/// <param name="members">
/// The members the handler watches, as <c>nameof</c> gives them.
/// </param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class ReactiveMembersAttribute(params string[] members) : Attribute
{
    /// <summary>
    /// The members the handler watches.
    /// </summary>
    public string[] Members { get; init; } = members;
}
