using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Filtering;
using Shoko.Server.Extensions;

#nullable enable
namespace Shoko.Server.Filters;

public class FilterExpression : IFilterExpression, IEquatable<FilterExpression>
{
    [IgnoreDataMember, JsonIgnore]
    public virtual bool TimeDependent => false;

    [IgnoreDataMember, JsonIgnore]
    public virtual bool UserDependent => false;

    [IgnoreDataMember, JsonIgnore]
    public virtual bool Deprecated => false;

    [IgnoreDataMember, JsonIgnore]
    public virtual string Name =>
        GetType().Name.TrimEnd("Expression").TrimEnd("Function").TrimEnd("SortingSelector").TrimEnd("Selector").CamelCaseToNatural();

    [IgnoreDataMember, JsonIgnore]
    public virtual FilterExpressionGroup Group => FilterExpressionGroup.Info;

    [IgnoreDataMember, JsonIgnore]
    public virtual string HelpDescription => string.Empty;

    [IgnoreDataMember, JsonIgnore]
    public virtual string[][] HelpPossibleParameterPairs => [];

    [IgnoreDataMember, JsonIgnore]
    public virtual string[] HelpPossibleParameters => [];

    [IgnoreDataMember, JsonIgnore]
    public virtual string[] HelpPossibleSecondParameters => [];

    public virtual bool Equals(FilterExpression? obj)
        => obj is not null && ReferenceEquals(this, obj);

    public override bool Equals(object? obj)
        => obj is not null && ReferenceEquals(this, obj);

    public override int GetHashCode()
        => GetType().FullName!.GetHashCode();

    public static bool operator ==(FilterExpression? left, FilterExpression? right)
        => left?.Equals((object?)right) ?? right is null;

    public static bool operator !=(FilterExpression? left, FilterExpression? right)
        => !left?.Equals((object?)right) ?? right is not null;

    public virtual bool IsType(FilterExpression expression)
        => expression.GetType() == GetType();
}

public abstract class FilterExpression<T> : FilterExpression, IFilterExpression<T>
{
    public abstract T Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time);
}
