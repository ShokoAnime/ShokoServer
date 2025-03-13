using System;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Config.Attributes;

/// <summary>
/// Used to mark a property as a code editor in the specified language in the UI.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class CodeEditorAttribute : Attribute
{
    /// <summary>
    /// The code language for the editor to use.
    /// </summary>
    public CodeLanguage Language { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeEditorAttribute"/> class with <see cref="CodeLanguage.PlainText"/> as the default language.
    /// </summary>
    public CodeEditorAttribute()
    {
        Language = CodeLanguage.PlainText;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeEditorAttribute"/> class with the specified <see cref="CodeLanguage"/>.
    /// </summary>
    /// <param name="language">The code language for the editor to use.</param>
    public CodeEditorAttribute(CodeLanguage language)
    {
        Language = language;
    }
}
