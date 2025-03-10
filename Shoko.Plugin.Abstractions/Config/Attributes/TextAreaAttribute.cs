
using System;

namespace Shoko.Plugin.Abstractions.Config.Attributes;

/// <summary>
/// Used to mark a property as a text-area in the UI.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class TextAreaAttribute() : Attribute { }
