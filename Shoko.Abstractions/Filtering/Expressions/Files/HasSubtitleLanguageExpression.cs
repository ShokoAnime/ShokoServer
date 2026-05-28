using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;

namespace Shoko.Abstractions.Filtering.Expressions.Files;

/// <summary>
/// This condition passes if any of the files have the specified subtitle language
/// </summary>
public class HasSubtitleLanguageExpression : FilterExpression<bool>, IWithStringParameter
{
    /// <inheritdoc/>
    public HasSubtitleLanguageExpression(string parameter)
        => Parameter = parameter;

    /// <inheritdoc/>
    public HasSubtitleLanguageExpression() { }

    /// <inheritdoc/>
    public string? Parameter { get; set; }

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if any of the files have the specified subtitle language";

    /// <inheritdoc/>
    public override string[] HelpPossibleParameters => PossibleSubtitleLanguages;

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return Parameter is not null && filterable.SubtitleLanguages.Contains(Parameter);
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(HasSubtitleLanguageExpression other)
    {
        return base.Equals(other) && Parameter == other.Parameter;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is null)
            return false;

        if (ReferenceEquals(this, obj))
            return true;

        if (obj.GetType() != GetType())
            return false;

        return Equals((HasSubtitleLanguageExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Parameter);

    /// <summary>
    /// Possible values for the parameter.
    /// </summary>
    public static readonly string[] PossibleSubtitleLanguages =
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
        "chinese (simplified)",
        "chinese (traditional)",
        "chinese (transcription)",
        "greek (ancient)",
        "japanese (transcription)",
        "korean (transcription)",
        "thai (transcription)",
        "urdu",
        "unknown",
        "other",
    };
}
