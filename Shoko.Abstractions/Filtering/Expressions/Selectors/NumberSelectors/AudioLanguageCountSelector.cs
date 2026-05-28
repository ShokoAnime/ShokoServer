using System;

namespace Shoko.Abstractions.Filtering.Expressions.Selectors.NumberSelectors;

/// <summary>
/// This returns how many distinct audio languages are present in all of the files in a filterable
/// </summary>
public class AudioLanguageCountSelector : FilterExpression<double>
{

    /// <inheritdoc/>
    public override string HelpDescription => "This returns how many distinct audio languages are present in all of the files in a filterable";
    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    /// <inheritdoc/>
    public override double Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.AudioLanguages.Count;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(AudioLanguageCountSelector other)
    {
        return base.Equals(other);
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

        return Equals((AudioLanguageCountSelector)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
