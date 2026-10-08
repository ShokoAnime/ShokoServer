using System;

namespace Shoko.Abstractions.UI.Attributes;

/// <summary>
/// Offers the values a member may take as options the server lists on request,
/// from a method on the class declaring the member.
/// </summary>
/// <remarks>
/// <para>
/// The method is public, static or not, and named without overloads. It returns
/// a collection of the member's value type, or of
/// <see cref="Components.SelectOption{TValue}"/> of it to label each option,
/// directly or through a <see cref="System.Threading.Tasks.Task{TResult}"/> or
/// <see cref="System.Threading.Tasks.ValueTask{TResult}"/>. A collection member
/// is offered options for its entries, and a nullable one for the type it wraps.
/// </para>
/// <para>
/// Its parameters are filled in the way a custom action's are: the
/// configuration or action being edited, the user and anything registered as a
/// service. A member that cannot take options, or a method that does not fit,
/// fails startup; the SHOKO0008 analyzer rule reports the same at compile time.
/// </para>
/// </remarks>
/// <param name="methodName">
/// The name of the method listing the options, as <c>nameof</c> gives it.
/// </param>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class OptionsProviderAttribute(string methodName) : Attribute
{
    /// <summary>
    /// The name of the method listing the options.
    /// </summary>
    public string MethodName { get; } = methodName;
}
