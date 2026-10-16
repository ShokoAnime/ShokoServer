using System;
using System.Text.RegularExpressions;
using System.Threading;

namespace Shoko.Abstractions.Utilities;

/// <summary>
/// A regular expression from outside the server's control, such as a user's
/// filter or search, an admin's setting or a plugin's schema, that stops being
/// run once a match has run out of time.
/// </summary>
/// <remarks>
/// A tripped pattern answers every later match with no match at once, so a bad
/// pattern costs about one timeout in total.
/// </remarks>
internal sealed class BoundedRegex
{
    private readonly Regex _regex;

    private int _tripped;

    /// <summary>
    /// Raised once per pattern, by the match that tripped it, with the pattern
    /// and the input it ran out of time on.
    /// </summary>
    internal static event Action<string, string>? Tripped;

    /// <summary>
    /// Compiles a pattern that may run for <see cref="RegexUtility.MatchTimeout"/>
    /// per match.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="options">The options to compile it with.</param>
    /// <exception cref="ArgumentException">The pattern is not a valid regular expression.</exception>
    internal BoundedRegex(string pattern, RegexOptions options = RegexOptions.None)
        : this(pattern, options, RegexUtility.MatchTimeout) { }

    /// <summary>
    /// Compiles a pattern with a match timeout of its own, for tests.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="options">The options to compile it with.</param>
    /// <param name="matchTimeout">How long one match may run.</param>
    /// <exception cref="ArgumentException">The pattern is not a valid regular expression.</exception>
    internal BoundedRegex(string pattern, RegexOptions options, TimeSpan matchTimeout)
        => _regex = new Regex(pattern, options, matchTimeout);

    /// <summary>
    /// The pattern.
    /// </summary>
    internal string Pattern => _regex.ToString();

    /// <summary>
    /// Whether a match has run out of time, so the pattern is no longer run.
    /// </summary>
    internal bool IsTripped => Volatile.Read(ref _tripped) is not 0;

    /// <summary>
    /// Checks whether the pattern matches the input, counting a match that
    /// runs out of time, and every match after it, as no match.
    /// </summary>
    /// <param name="input">The text to match.</param>
    /// <returns><c>true</c> if it matched in time, otherwise <c>false</c>.</returns>
    internal bool IsMatch(string input)
    {
        if (IsTripped)
            return false;

        try
        {
            return _regex.IsMatch(input);
        }
        catch (RegexMatchTimeoutException)
        {
            if (Interlocked.Exchange(ref _tripped, 1) is 0)
                Tripped?.Invoke(Pattern, input);
            return false;
        }
    }
}
