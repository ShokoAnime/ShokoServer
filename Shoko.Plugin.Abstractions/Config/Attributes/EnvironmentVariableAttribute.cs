using System;

namespace Shoko.Plugin.Abstractions.Config.Attributes;

/// <summary>
/// Indicates that the property/field can be loaded from an environment variable.
/// </summary>
/// <param name="name">The environment variable name.</param>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class EnvironmentVariableAttribute(string name) : Attribute()
{
    /// <summary>
    /// The environment variable name.
    /// </summary>
    public string Name { get; set; } = name;

    /// <summary>
    /// Indicates if the value can be changed even if it's loaded from an
    /// environment variable.
    /// </summary>
    public bool AllowOverride { get; set; }
}
