using System;

namespace Shoko.Plugin.Abstractions.Config.Attributes;

/// <summary>
/// Hides the default save action for the configuration in the UI. Useful for
/// configurations with custom actions, where one or more custom actions are
/// responsible for saving the configuration.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class HideDefaultSaveActionAttribute : Attribute { }
