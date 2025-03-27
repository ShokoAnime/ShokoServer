using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Files;

public class HasSubtitleLanguageExpression : FilterExpression<bool>, IWithStringParameter
{
    public HasSubtitleLanguageExpression(string parameter)
    {
        Parameter = parameter;
    }
    public HasSubtitleLanguageExpression() { }

    public string Parameter { get; set; }
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This condition passes if any of the files have the specified subtitle language";
    public override string[] HelpPossibleParameters => PossibleSubtitleLanguages;

    public override bool Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return filterable.SubtitleLanguages.Contains(Parameter);
    }

    protected bool Equals(HasSubtitleLanguageExpression other)
    {
        return base.Equals(other) && Parameter == other.Parameter;
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

        return Equals((HasSubtitleLanguageExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Parameter);
    }

    public static bool operator ==(HasSubtitleLanguageExpression left, HasSubtitleLanguageExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasSubtitleLanguageExpression left, HasSubtitleLanguageExpression right)
    {
        return !Equals(left, right);
    }
    public static readonly string[] PossibleSubtitleLanguages =
    {
        "english", "japanese",
        "japanese (transcription)", "afrikaans",
        "albanian", "arabic",
        "basque", "bengali",
        "bulgarian", "bosnian",
        "catalan", "chinese (unspecified)",
        "chinese (transcription)", "chinese (traditional)",
        "chinese (simplified)", "croatian",
        "czech", "danish",
        "dutch", "esperanto",
        "estonian", "filipino",
        "filipino (tagalog)", "finnish",
        "french", "galician",
        "georgian", "german",
        "greek", "greek (ancient)",
        "haitian creole", "hebrew",
        "hindi", "hungarian",
        "icelandic", "indonesian",
        "italian", "javanese",
        "korean", "korean (transcription)",
        "latin", "latvian",
        "lithuanian", "malay",
        "mongolian", "nepali",
        "norwegian", "persian",
        "polish", "portuguese",
        "portuguese (brazilian)", "romanian",
        "russian", "serbian",
        "sinhala", "slovak",
        "slovenian", "spanish",
        "spanish (latin american)", "swedish",
        "tamil", "tatar",
        "telugu", "thai",
        "thai (transcription)", "turkish",
        "ukrainian", "urdu",
        "vietnamese", "unknown",
        "other",
    };
}
