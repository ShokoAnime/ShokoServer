using System;

namespace Shoko.Plugin.Abstractions.Config.Attributes;

/// <summary>
/// Hides the save button for the section in the UI.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class HideSaveActionAttribute : Attribute { }
