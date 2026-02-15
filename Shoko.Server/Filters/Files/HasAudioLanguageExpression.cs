using System;
using Shoko.Abstractions.Filtering;
using Shoko.Server.Filters.Interfaces;

#nullable enable
namespace Shoko.Server.Filters.Files;

public class HasAudioLanguageExpression : FilterExpression<bool>, IWithStringParameter
{
    public string Parameter { get; set; }

    public override string HelpDescription => "This condition passes if any of the files have the specified audio language";

    public override string[] HelpPossibleParameters => PossibleAudioLanguages;

    public HasAudioLanguageExpression(string parameter)
        => Parameter = parameter;

    public HasAudioLanguageExpression()
        => Parameter = string.Empty;

    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.AudioLanguages.Contains(Parameter);
    }

    public bool Equals(HasAudioLanguageExpression? other)
        => other is not null && (
            ReferenceEquals(this, other) ||
            string.Equals(Parameter, other.Parameter, StringComparison.OrdinalIgnoreCase)
        );

    public override bool Equals(object? obj)
        => obj is not null && Equals(obj as HasAudioLanguageExpression);

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(base.GetHashCode());
        hashCode.Add(Parameter, StringComparer.InvariantCultureIgnoreCase);
        return hashCode.ToHashCode();
    }

    public static bool operator ==(HasAudioLanguageExpression left, HasAudioLanguageExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasAudioLanguageExpression left, HasAudioLanguageExpression right)
    {
        return !Equals(left, right);
    }

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
