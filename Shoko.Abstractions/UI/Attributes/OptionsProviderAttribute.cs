using System;
using Shoko.Abstractions.UI.Enums;

namespace Shoko.Abstractions.UI.Attributes;

/// <summary>
/// Marks a method as listing the values the named members may take, as options
/// the server lists on request. A method without it is never checked.
/// </summary>
/// <remarks>
/// <para>
/// The method is public, static or not, and not generic. It returns an array or
/// any <see cref="System.Collections.Generic.IEnumerable{T}"/> of the members'
/// option type, or of <see cref="Components.SelectOption{TValue}"/> of it to
/// label each option, directly or through a
/// <see cref="System.Threading.Tasks.Task{TResult}"/> or
/// <see cref="System.Threading.Tasks.ValueTask{TResult}"/>.
/// </para>
/// <para>
/// Every member it names is a property of the same class taking the same option
/// type for <see cref="Target"/>, exactly: a scalar itself, a list's entries, a
/// dictionary's values or keys, nullable types unwrapped. That type is a
/// primitive, a string, an enum, a common text round-trip type, or any type
/// implementing <see cref="IParsable{TSelf}"/> of itself or carrying a
/// <see cref="System.ComponentModel.TypeConverterAttribute"/> to and from text
/// or a primitive. Each part of a member has one provider at most.
/// </para>
/// <para>
/// A provider may refuse the draft by throwing
/// <see cref="Exceptions.GenericValidationException"/>. A provider that does
/// not fit fails startup and is reported by SHOKO0008 to SHOKO0013. The UI
/// README holds the full contract.
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

    /// <summary>
    /// Which part of the members the options are for. Defaults to their values.
    /// </summary>
    public OptionsTarget Target { get; set; }
}
