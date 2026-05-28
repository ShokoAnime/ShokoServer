using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;

namespace Shoko.Abstractions.Filtering.Expressions.Files;

/// <summary>
/// This condition passes if all of the files have the specified audio language
/// </summary>
public class HasSharedAudioLanguageExpression : FilterExpression<bool>, IWithStringParameter
{
    /// <inheritdoc/>
    public HasSharedAudioLanguageExpression(string parameter)
        => Parameter = parameter;

    /// <inheritdoc/>
    public HasSharedAudioLanguageExpression() { }

    /// <inheritdoc/>
    public string? Parameter { get; set; }

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if all of the files have the specified audio language";

    /// <inheritdoc/>
    public override string[] HelpPossibleParameters => HasAudioLanguageExpression.PossibleAudioLanguages;

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return Parameter is not null && filterable.SharedAudioLanguages.Contains(Parameter);
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(HasSharedAudioLanguageExpression other)
    {
        return base.Equals(other) && string.Equals(Parameter, other.Parameter, StringComparison.InvariantCultureIgnoreCase);
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

        return Equals((HasSharedAudioLanguageExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Parameter);
}
