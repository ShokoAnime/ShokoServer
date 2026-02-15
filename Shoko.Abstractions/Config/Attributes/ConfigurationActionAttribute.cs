using System;
using Shoko.Abstractions.Config.Enums;

namespace Shoko.Abstractions.Config.Attributes;

/// <summary>
///   Attribute used for marking a method as a reactive configuration action.
/// /// </summary>
/// <param name="actionType">
///   The type of action to perform on the configuration when this method is
///   called.
/// </param>
[AttributeUsage(AttributeTargets.Method)]
public class ConfigurationActionAttribute(ConfigurationActionType actionType) : Attribute()
{
    /// <summary>
    ///   The type of action to perform on the configuration when this method is
    ///   called.
    /// </summary>
    public ConfigurationActionType ActionType { get; set; } = actionType;

    /// <summary>
    ///   The reactive event type this action handles.
    /// </summary>
    public ReactiveEventType ReactiveEventType { get; set; } = ReactiveEventType.All;
}
