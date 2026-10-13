using System;

namespace Shoko.Abstractions.UI.Attributes;

/// <summary>
/// Marks the parameter of an <see cref="OptionsProviderAttribute"/> method that
/// receives the key of the dictionary entry the options are asked for, so the
/// values can be narrowed per key.
/// </summary>
/// <remarks>
/// <para>
/// Only a provider of a dictionary's values may take one, and the parameter's
/// type is the dictionary's key type, exactly. The key comes from the path
/// asked with, such as <c>Weights["key"]</c>, so it need not be in the
/// dictionary yet. A provider without the parameter lists the same values for
/// every key.
/// </para>
/// <para>
/// A provider that does not fit fails startup and is reported by SHOKO0015.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class OptionsKeyAttribute : Attribute;
