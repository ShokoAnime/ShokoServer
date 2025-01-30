
namespace Shoko.Plugin.Abstractions.Enums;

/// <summary>
/// Types of settings that can be used on the settings object of a
/// <see cref="IRenamer{T}"/>.
/// </summary>
public enum RenamerSettingType
{
    /// <summary>
    /// Auto is a special setting where it figures out what type of setting it 
    /// is using type reflection.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// A code setting is a setting that requires a special code to be executed.
    /// </summary>
    Code = 1,

    /// <summary>
    /// A text setting is a setting that contains a string of text.
    /// </summary>
    Text = 2,

    /// <summary>
    /// A large text setting is a setting that contains a multi-line string of text.
    /// </summary>
    LargeText = 3,

    /// <summary>
    /// An integer setting is a setting that contains a number.
    /// </summary>
    Integer = 4,

    /// <summary>
    /// A decimal setting is a setting that contains a decimal number.
    /// </summary>
    Decimal = 5,

    /// <summary>
    /// A boolean setting is a setting that contains a true or false value.
    /// </summary>
    Boolean = 6,
}
