using System;
using Shoko.Abstractions.Enums;

namespace Shoko.Abstractions.Metadata;

/// <summary>
///   Represents a title from a data source.
/// </summary>
public interface ITitle : IText, IEquatable<ITitle>
{
    /// <summary>
    ///   The title type.
    /// </summary>
    TitleType Type { get; }

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
