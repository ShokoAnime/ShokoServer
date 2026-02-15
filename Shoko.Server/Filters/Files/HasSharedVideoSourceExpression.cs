using System;
using Shoko.Abstractions.Filtering;
using Shoko.Server.Filters.Interfaces;

#nullable enable
namespace Shoko.Server.Filters.Files;

public class HasSharedVideoSourceExpression : FilterExpression<bool>, IWithStringParameter, IEquatable<HasSharedVideoSourceExpression>
{
    public string Parameter { get; set; }

    public override string HelpDescription => "This condition passes if all of the files have the specified video source";

    public override string[] HelpPossibleParameters =>
    [
        "tv",
        "www",
        "dvd",
        "bluray",
        "vhs",
        "camcorder",
        "vcd",
        "ld",
        "unk",
    ];

    public HasSharedVideoSourceExpression(string parameter)
        => Parameter = parameter;

    public HasSharedVideoSourceExpression()
        => Parameter = string.Empty;

    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
        => filterable.SharedVideoSources.Contains(Parameter);

    public bool Equals(HasSharedVideoSourceExpression? other)
        => other is not null && (
            ReferenceEquals(this, other) ||
            Parameter == other.Parameter
        );

    public override bool Equals(object? obj)
        => obj is not null && Equals(obj as HasSharedVideoSourceExpression);

    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Parameter);
}
