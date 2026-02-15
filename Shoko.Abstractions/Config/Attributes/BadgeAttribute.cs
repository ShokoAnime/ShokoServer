using System;
using Shoko.Abstractions.Config.Enums;

namespace Shoko.Abstractions.Config.Attributes;

/// <summary>
/// Controls the displayed badge of a property/field in the UI.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class BadgeAttribute : Attribute
{
    /// <summary>
    /// Determines the color theme of the badge in the UI.
    /// </summary>
    public DisplayColorTheme Theme { get; set; }

    /// <summary>
    /// Gets or sets the name of the badge in the UI.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BadgeAttribute"/> class.
    /// </summary>
    /// <param name="badge">The badge of the property/field.</param>
    public BadgeAttribute(string badge) => Name = badge;
}
