using System;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   Thrown when adding an alias that another channel of the same type already
///   holds, either as its own name or as one of its aliases. For a given type a
///   normalised name resolves to at most one thing, so the alias is rejected
///   instead of moved.
/// </summary>
/// <param name="alias">
///   The alias that could not be added, as it was given.
/// </param>
/// <param name="conflictingChannel">
///   The channel already holding the name.
/// </param>
/// <param name="heldAsOwnName">
///   Whether <paramref name="conflictingChannel"/> holds the name as its own
///   name rather than as an alias.
/// </param>
/// <param name="paramName">
///   Optional. The name of the parameter that caused the exception.
/// </param>
public class ChannelAliasConflictException(string alias, IAiringChannel conflictingChannel, bool heldAsOwnName, string? paramName = null)
    : ArgumentException(
        $"The alias \"{alias}\" is already held by the channel \"{conflictingChannel.Name}\" as {(heldAsOwnName ? "its own name" : "an alias")}.",
        paramName
    )
{
    /// <summary>
    ///   The alias that could not be added, as it was given.
    /// </summary>
    public string Alias { get; } = alias;

    /// <summary>
    ///   The channel already holding the name, so a bulk seeder can skip this
    ///   one and carry on.
    /// </summary>
    public IAiringChannel ConflictingChannel { get; } = conflictingChannel;

    /// <summary>
    ///   Whether <see cref="ConflictingChannel"/> holds the name as its own
    ///   name rather than as one of its aliases.
    /// </summary>
    public bool HeldAsOwnName { get; } = heldAsOwnName;
}
