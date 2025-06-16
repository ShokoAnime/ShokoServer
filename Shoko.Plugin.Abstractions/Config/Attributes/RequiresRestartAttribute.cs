using System;

namespace Shoko.Plugin.Abstractions.Config.Attributes;

/// <summary>
/// Define that a configuration requires a restart to take effect.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class RequiresRestartAttribute : Attribute { }
