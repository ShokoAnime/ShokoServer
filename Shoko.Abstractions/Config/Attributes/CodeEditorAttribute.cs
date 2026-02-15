using System;
using Shoko.Abstractions.Config.Enums;

namespace Shoko.Abstractions.Config.Attributes;

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
    public CodeEditorLanguage Language { get; set; }

    /// <summary>
    /// Whether to automatically format the code/text on load.
    /// </summary>
    public bool AutoFormatOnLoad { get; set; }

    /// <summary>
    /// The height of the text-area in the UI.
    /// </summary>
    public DisplayElementSize Height { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeEditorAttribute"/> class with the specified <see cref="CodeEditorLanguage"/>.
    /// </summary>
    /// <param name="language">The code language for the editor to use.</param>
    public CodeEditorAttribute(CodeEditorLanguage language) => Language = language;
}
