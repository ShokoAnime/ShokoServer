using System.Runtime.Serialization;

namespace Shoko.Abstractions.Config.Enums;

/// <summary>
/// Types of configuration actions that can be performed on a configuration.
/// </summary>
public enum ConfigurationActionType
{
    /// <summary>
    ///   A new configuration is being initialized by this action.
    /// </summary>
    [EnumMember(Value = "new")]
    New = 0,

    /// <summary>
    ///   The configuration is to be validated by this action.
    /// </summary>
    [EnumMember(Value = "validate")]
    Validate = 1,

    /// <summary>
    ///   The configuration is about to be saved by this action.
    /// </summary>
    [EnumMember(Value = "save")]
    Save = 2,

    /// <summary>
    ///   The configuration is about to be loaded by this action.
    /// </summary>
    [EnumMember(Value = "load")]
    Load = 3,

    /// <summary>
    ///   The configuration was changed in the UI without being saved.
    /// </summary>
    [EnumMember(Value = "live-edit")]
    LiveEdit = 4,
}
