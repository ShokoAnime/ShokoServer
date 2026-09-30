using System;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata;

/// <summary>
///   Represents a title from a metadata source.
/// </summary>
public interface ITitle : IText, IEquatable<ITitle>
{
    /// <summary>
    ///   The title type.
    /// </summary>
    TitleType Type { get; }

    /// <summary>
    ///   Whether the title was made up on the spot, such as <c>Episode 5</c>
    ///   for an episode no source named. A made-up title is never stored and
    ///   is only chosen when nothing else is left.
    /// </summary>
    /// <value>
    ///   <c>true</c> for a made-up title; otherwise <c>false</c>.
    /// </value>
    bool IsSynthesized { get => false; }

    /// <summary>
    ///   Checks if two title objects are equal.
    /// </summary>
    /// <param name="titleA">
    ///   The first title.
    /// </param>
    /// <param name="titleB">
    ///   The second title.
    /// </param>
    /// <returns>
    ///   <c>true</c> if the titles are equal; otherwise, <c>false</c>.
    /// </returns>
    public static bool Equals(ITitle? titleA, ITitle? titleB)
        => titleA is not null && titleB is not null && (
            ReferenceEquals(titleA, titleB) || (
                titleA.Source == titleB.Source &&
                titleA.Type == titleB.Type &&
                titleA.Language == titleB.Language &&
                string.Equals(titleA.CountryCode, titleB.CountryCode) &&
                string.Equals(titleA.Value, titleB.Value)
            )
        );
}
