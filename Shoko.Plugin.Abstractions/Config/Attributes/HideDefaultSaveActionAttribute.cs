using System;

namespace Shoko.Plugin.Abstractions.Config.Attributes;

/// <summary>
/// Hides the default save action for the configuration in the UI.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class HideDefaultSaveActionAttribute : Attribute { }
