using System;

namespace Shoko.Abstractions.UI.Attributes;

/// <summary>
/// Marks a method as listing the values the named members may take, as options
/// the server lists on request.
/// </summary>
/// <remarks>
/// <para>
/// The method is public, static or not, and not generic. Every member it names
/// is a property of the same class and takes the same option type: a collection
/// offers options for its entries, and a nullable member for the type it wraps.
/// It returns a collection of that type, or of
/// <see cref="Components.SelectOption{TValue}"/> of it to label each option,
/// directly or through a <see cref="System.Threading.Tasks.Task{TResult}"/> or
/// <see cref="System.Threading.Tasks.ValueTask{TResult}"/>. A member has one
/// provider at most.
/// </para>
/// <para>
/// Its parameters are filled in the way a custom action's are, or for an
/// executable action the way its execution is. A provider that does not fit
/// fails startup; the SHOKO0008 analyzer rule reports the same at compile time.
/// </para>
/// </remarks>
/// <param name="members">
/// The members the method lists options for, as <c>nameof</c> gives them.
/// </param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class OptionsProviderAttribute(params string[] members) : Attribute
{
    /// <summary>
    /// The members the method lists options for.
    /// </summary>
    public string[] Members { get; } = members;
}
