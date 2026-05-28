using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;

namespace Shoko.Abstractions.Filtering.Expressions.Files;

/// <summary>
/// This condition passes if all of the files have the specified video source
/// </summary>
public class HasSharedVideoSourceExpression : FilterExpression<bool>, IWithStringParameter, IEquatable<HasSharedVideoSourceExpression>
{

    /// <inheritdoc/>
    public HasSharedVideoSourceExpression(string parameter)
        => Parameter = parameter;

    /// <inheritdoc/>
    public HasSharedVideoSourceExpression() { }

    /// <inheritdoc/>
    public string? Parameter { get; set; }

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if all of the files have the specified video source";

    /// <inheritdoc/>
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
        "film",
        "unk",
    ];

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
        => Parameter is not null && filterable.SharedVideoSources.Contains(Parameter);

    /// <inheritdoc cref="Equals(object)"/>
    public bool Equals(HasSharedVideoSourceExpression? other)
        => other is not null && (
            ReferenceEquals(this, other) ||
            Parameter == other.Parameter
        );

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is not null && Equals(obj as HasSharedVideoSourceExpression);

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Parameter);
}
