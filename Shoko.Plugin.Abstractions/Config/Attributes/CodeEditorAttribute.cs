using System;
using Shoko.Plugin.Abstractions.Config.Enums;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Config.Attributes;

/// <summary>
/// Used to mark a property/field as a code editor in the specified language in
/// the UI.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class CodeEditorAttribute : Attribute
{
    /// <summary>
    /// The code language for the editor to use.
    /// </summary>
    public CodeLanguage Language { get; set; }

    /// <summary>
    /// Whether to automatically format the code/text on load.
    /// </summary>
    public bool AutoFormatOnLoad { get; set; }

    /// <summary>
    /// The height of the text-area in the UI.
    /// </summary>
    public DisplayElementSize Height { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeEditorAttribute"/> class with the specified <see cref="CodeLanguage"/>.
    /// </summary>
    /// <param name="language">The code language for the editor to use.</param>
    public CodeEditorAttribute(CodeLanguage language) => Language = language;
}
