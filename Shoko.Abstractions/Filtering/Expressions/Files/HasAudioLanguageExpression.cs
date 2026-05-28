using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;

namespace Shoko.Abstractions.Filtering.Expressions.Files;

/// <summary>
/// This condition passes if any of the files have the specified audio language
/// </summary>
public class HasAudioLanguageExpression : FilterExpression<bool>, IWithStringParameter
{
    /// <inheritdoc/>
    public HasAudioLanguageExpression(string parameter)
        => Parameter = parameter;

    /// <inheritdoc/>
    public HasAudioLanguageExpression() { }

    /// <inheritdoc/>
    public string? Parameter { get; set; }

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if any of the files have the specified audio language";

    /// <inheritdoc/>
    public override string[] HelpPossibleParameters => PossibleAudioLanguages;

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return Parameter is not null && filterable.AudioLanguages.Contains(Parameter);
    }

    /// <inheritdoc cref="Equals(object)"/>
    public bool Equals(HasAudioLanguageExpression? other)
        => other is not null && (
            ReferenceEquals(this, other) ||
            string.Equals(Parameter, other.Parameter, StringComparison.OrdinalIgnoreCase)
        );

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is not null && Equals(obj as HasAudioLanguageExpression);

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Parameter);

    /// <summary>
    /// Possible values for the parameter.
    /// </summary>
    public static readonly string[] PossibleAudioLanguages =
    {
        "afrikaans",
        "albanian",
        "arabic",
        "basque",
        "bengali",
        "bosnian",
        "bulgarian",
        "burmese",
        "catalan",
        "chinese",
        "croatian",
        "czech",
        "danish",
        "dutch",
        "english",
        "esperanto",
        "estonian",
        "filipino",
        "tagalog",
        "finnish",
        "french",
        "galician",
        "georgian",
        "german",
        "greek",
        "haitian creole",
        "hebrew",
        "hindi",
        "hungarian",
        "icelandic",
        "indonesian",
        "italian",
        "japanese",
        "javanese",
        "korean",
        "latin",
        "latvian",
        "lithuanian",
        "malay",
        "mongolian",
        "nepali",
        "norwegian",
        "persian",
        "polish",
        "portuguese",
        "portuguese (brazilian)",
        "romanian",
        "russian",
        "serbian",
        "sinhala",
        "slovak",
        "slovenian",
        "spanish",
        "spanish (latin american)",
        "swedish",
        "tamil",
        "tatar",
        "telugu",
        "thai",
        "turkish",
        "ukrainian",
        "vietnamese",
        "cantonese",
        "mandarin",
        "taiwanese",
        "instrumental",
        "unknown",
        "other",
    };
}
