using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Files;

public class HasAudioLanguageExpression : FilterExpression<bool>, IWithStringParameter
{
    public HasAudioLanguageExpression(string parameter)
    {
        Parameter = parameter;
    }
    public HasAudioLanguageExpression() { }

    public string Parameter { get; set; }
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This condition passes if any of the files have the specified audio language";
    public override string[] HelpPossibleParameters => PossibleAudioLanguages;

    public override bool Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return filterable.AudioLanguages.Contains(Parameter);
    }

    protected bool Equals(HasAudioLanguageExpression other)
    {
        return base.Equals(other) && string.Equals(Parameter, other.Parameter, StringComparison.InvariantCultureIgnoreCase);
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != this.GetType())
        {
            return false;
        }

        return Equals((HasAudioLanguageExpression)obj);
    }

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
