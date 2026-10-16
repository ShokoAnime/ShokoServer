using System;
using System.Text.RegularExpressions;

namespace Shoko.Abstractions.Utilities;

/// <summary>
/// Bounds how long a regular expression may run against one input.
/// </summary>
internal static class RegexUtility
{
    /// <summary>
    /// How long one match may run, in milliseconds, for
    /// <see cref="GeneratedRegexAttribute"/>.
    /// </summary>
    /// <remarks>
    /// Two seconds, the default .NET's own <c>RegularExpressionAttribute</c>
    /// gives <c>MatchTimeoutInMilliseconds</c> in its constructor (see
    /// <see href="https://source.dot.net/System.ComponentModel.Annotations/System/ComponentModel/DataAnnotations/RegularExpressionAttribute.cs.html"/>).
    /// Long enough that a busy machine never times out an honest match; a
    /// pattern that times out once stops being run (<see cref="BoundedRegex"/>).
    /// </remarks>
    internal const int MatchTimeoutMilliseconds = 2000;

    /// <summary>
    /// How long one match may run.
    /// </summary>
    internal static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(MatchTimeoutMilliseconds);
}
