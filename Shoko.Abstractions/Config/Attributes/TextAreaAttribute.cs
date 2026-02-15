using System;

namespace Shoko.Abstractions.Config.Attributes;

/// <summary>
/// Used to mark a property/field as a text-area in the UI.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class TextAreaAttribute() : Attribute { }
