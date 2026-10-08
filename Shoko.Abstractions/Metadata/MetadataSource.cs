using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Shoko.Abstractions.Metadata.Converters;

namespace Shoko.Abstractions.Metadata;

/// <summary>
///   A source of metadata, such as AniDB, TMDB or a plugin's own source,
///   named by a kebab-case <see cref="Value"/>. Sources are registered up
///   front and shared; text is turned into a source with <see cref="Get"/>,
///   <see cref="TryGet"/> or <see cref="Parse(string, IFormatProvider?)"/>,
///   which accept a value or an alias, ignoring case and reading <c>_</c> as
///   <c>-</c>.
/// </summary>
/// <remarks>
///   Registration closes once every plugin is set up. A plugin registers its
///   sources from the static constructor of a class it touches in
///   <c>IPluginServiceRegistration.RegisterServices</c> (<c>IPlugin.Setup</c>
///   at the latest). A source another plugin owns is looked up with
///   <see cref="TryGet"/> on every access, never cached. Sources chosen by
///   settings apply on the next start (<see cref="Core.Services.ISystemService.RequireRestart{TPlugin}(string)"/>).
/// </remarks>
[TypeConverter(typeof(MetadataSourceTypeConverter))]
[System.Text.Json.Serialization.JsonConverter(typeof(MetadataSourceJsonConverter))]
[Newtonsoft.Json.JsonConverter(typeof(MetadataSourceNewtonsoftJsonConverter))]
public sealed partial class MetadataSource : IEquatable<MetadataSource>, IComparable<MetadataSource>, IComparable, IParsable<MetadataSource>
{
    #region Sort Order

    /// <summary>
    ///   The sort rank of every value the old enum didn't have: after its
    ///   numbered remote sources, before <c>plugin</c> and the local sources.
    /// </summary>
    private const int NewValueRank = 0x0E;

    /// <summary>
    ///   The sort rank of each value the old enum had, which is the number
    ///   the enum gave it, so sources keep sorting in the order they did.
    /// </summary>
    private static readonly FrozenDictionary<string, byte> _sortRanks = new Dictionary<string, byte>
    {
        ["anidb"] = 0x00,
        ["tmdb"] = 0x01,
        ["tvdb"] = 0x02,
        ["anilist"] = 0x03,
        ["animeshon"] = 0x04,
        ["kitsu"] = 0x05,
        ["mal"] = 0x06,
        ["fanart-tv"] = 0x07,
        ["imdb"] = 0x08,
        ["omdb"] = 0x09,
        ["trakt"] = 0x0A,
        ["tpdb"] = 0x0B,
        ["mediux"] = 0x0C,
        ["simkl"] = 0x0D,
        ["plugin"] = 0xFB,
        ["generated"] = 0xFC,
        ["user"] = 0xFE,
        ["shoko"] = 0xFF,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    #endregion

    #region Registry

    /// <summary>
    ///   The start of the value a stored number that names no source reads
    ///   back as, e.g. <c>unknown-253</c>. No registered key may start with it.
    /// </summary>
    internal const string UnknownPrefix = "unknown-";

    private static readonly Lock _registryLock = new();

    private static readonly ConcurrentDictionary<string, MetadataSource> _registeredByValue = new(StringComparer.Ordinal);

    /// <summary>
    ///   Every registered source under each of its keys: its value and its
    ///   aliases, ignoring case and reading <c>_</c> as <c>-</c>.
    /// </summary>
    private static readonly ConcurrentDictionary<string, MetadataSource> _registeredByKey = new(MetadataKeyComparer.Instance);

    private static readonly ConcurrentDictionary<string, MetadataSource> _unregisteredByValue = new(StringComparer.Ordinal);

    private static MetadataSource[] _all = [];

    private static volatile bool _frozen;

    /// <summary>
    ///   Raised after a source is registered, or after a registration merged
    ///   new aliases or a description into an existing source. The flag tells
    ///   which.
    /// </summary>
    internal static event Action<MetadataSource, bool>? Registered;

    static MetadataSource()
    {
        Shoko = RegisterCore("Shoko", "shoko", [], true, "Data the server makes or keeps on its own.");
        User = RegisterCore("User", "user", [], true, "Data a user of the server entered by hand.");
        Generated = RegisterCore("Locally Generated", "generated", ["locally-generated", "locallygenerated"], true,
            "Data synthesized on the server rather than entered by a person, such as thumbnails and stand-in episode titles.");
        AniDB = RegisterCore("AniDB", "anidb", [], false, "The anime database at anidb.net, which every series in Shoko is built on.");
        // Pre-registered, but kept like any other source's: in the shared stores.
        TMDB = RegisterInternal("TMDB", "tmdb", ["themoviedb"], false, "The Movie Database at themoviedb.org, for shows, movies and their artwork.", core: false);
    }

    /// <summary>
    ///   Shoko itself, for data the server makes or keeps on its own.
    /// </summary>
    public static MetadataSource Shoko { get; }

    /// <summary>
    ///   A user of the server, for data a person entered by hand.
    /// </summary>
    public static MetadataSource User { get; }

    /// <summary>
    ///   Data synthesized on the server rather than entered by a person: images
    ///   such as thumbnails a plugin uploads without a user, and the stand-in
    ///   titles given to episodes and seasons no source named, such as
    ///   <c>Episode 5</c> or <c>Specials</c>. Shown as <c>Locally Generated</c>.
    /// </summary>
    public static MetadataSource Generated { get; }

    /// <summary>
    ///   AniDB.
    /// </summary>
    public static MetadataSource AniDB { get; }

    /// <summary>
    ///   The Movie Database (TMDB).
    /// </summary>
    public static MetadataSource TMDB { get; }

    /// <summary>
    ///   Every registered source, the core's and the plugins', in the order
    ///   they were registered.
    /// </summary>
    public static IReadOnlyList<MetadataSource> All => Volatile.Read(ref _all);

    /// <summary>
    ///   Whether registration has closed, which happens once every plugin is
    ///   set up.
    /// </summary>
    internal static bool IsFrozen => _frozen;

    /// <summary>
    ///   Registers a source, or merges into the source already registered
    ///   under <paramref name="value"/>, and returns its one instance. A merge
    ///   adds the new aliases, keeps the first name, and keeps the first
    ///   description unless it was empty. Closes once every plugin is set up.
    /// </summary>
    /// <remarks>
    ///   Call it from the static constructor of a class holding the plugin's
    ///   own sources, and touch that class from
    ///   <c>IPluginServiceRegistration.RegisterServices</c>. A source another
    ///   plugin registers is looked up with <see cref="TryGet"/> on every
    ///   access instead; see the remarks on <see cref="MetadataSource"/>.
    /// </remarks>
    /// <param name="name">
    ///   The display name, e.g. <c>Example Artwork</c>. Only shown, never accepted
    ///   as input; it may hold spaces and need not be unique. At most 64
    ///   characters.
    /// </param>
    /// <param name="value">
    ///   The kebab-case value, e.g. <c>example-artwork</c>. At most 64 characters.
    /// </param>
    /// <param name="aliases">
    ///   Other spellings accepted as input, e.g. <c>artwork</c>. No whitespace.
    /// </param>
    /// <param name="local">
    ///   Whether the source's data is made on this server, not fetched from a
    ///   remote service.
    /// </param>
    /// <param name="description">
    ///   A short, plain description of the source, for display. Optional.
    /// </param>
    /// <returns>The registered source.</returns>
    /// <exception cref="ArgumentException">
    ///   The name, value or an alias is invalid; the value or an alias is
    ///   another source's value or alias, was already used as a source of its
    ///   own, starts with <c>unknown-</c>, or is one of the words the API's
    ///   <c>/api/v3/Metadata</c> routes keep for themselves (<c>provider</c>,
    ///   <c>entry</c> and <c>episode</c>); the local flag differs from an
    ///   earlier registration; or the registration would change a core source.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Registration closed after plugin setup. A source that depends on a
    ///   setting changed later is registered on the next start; raise a
    ///   restart reason with
    ///   <see cref="Core.Services.ISystemService.RequireRestart{TPlugin}(string)"/> instead.
    /// </exception>
    public static MetadataSource Register(string name, string value, IReadOnlyList<string>? aliases = null, bool local = false, string? description = null)
        => RegisterInternal(name, value, aliases ?? [], local, description, core: false);

    /// <summary>
    ///   Finds a registered source by its value or an alias, ignoring case
    ///   and reading <c>_</c> as <c>-</c>.
    /// </summary>
    /// <param name="text">The value or alias.</param>
    /// <param name="source">The registered source, if found.</param>
    /// <returns><c>true</c> if a registered source was found.</returns>
    public static bool TryGet([NotNullWhen(true)] string? text, [NotNullWhen(true)] out MetadataSource? source)
    {
        source = null;
        return !string.IsNullOrWhiteSpace(text) && _registeredByKey.TryGetValue(text.Trim(), out source);
    }

    /// <summary>
    ///   Gets a registered source by its value or an alias, ignoring case and
    ///   reading <c>_</c> as <c>-</c>.
    /// </summary>
    /// <param name="text">The value or alias.</param>
    /// <returns>The registered source.</returns>
    /// <exception cref="KeyNotFoundException">
    ///   No source is registered under <paramref name="text"/>.
    /// </exception>
    public static MetadataSource Get(string text)
        => TryGet(text, out var source) ? source : throw new KeyNotFoundException($"No metadata source is registered as \"{text}\".");

    /// <summary>
    ///   Turns text into a source: a registered source's value or alias gives
    ///   that source, ignoring case and reading <c>_</c> as <c>-</c>, and any
    ///   other text that is a valid value once lowercased and hyphenated
    ///   gives an unregistered source.
    /// </summary>
    /// <param name="s">The text.</param>
    /// <param name="provider">Ignored.</param>
    /// <returns>The source.</returns>
    /// <exception cref="FormatException">
    ///   <paramref name="s"/> is neither known nor a valid value.
    /// </exception>
    public static MetadataSource Parse(string s, IFormatProvider? provider = null)
        => TryParse(s, provider, out var source) ? source : throw new FormatException($"\"{s}\" is not a valid metadata source.");

    /// <summary>
    ///   Turns text into a source, as <see cref="Parse(string, IFormatProvider?)"/>
    ///   does, without throwing.
    /// </summary>
    /// <param name="s">The text.</param>
    /// <param name="provider">Ignored.</param>
    /// <param name="result">The source, if the text was valid.</param>
    /// <returns><c>true</c> if the text named a source.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out MetadataSource result)
    {
        if (TryGet(s, out result))
            return true;

        var value = s is null ? null : ToCandidateValue(s);
        if (!IsValidValue(value))
        {
            result = null;
            return false;
        }

        // Checked again under the lock: a registration may have taken the text
        // meanwhile. A value handed out here can no longer become another source's alias.
        lock (_registryLock)
        {
            result = _registeredByKey.TryGetValue(value, out var registered) ? registered : GetOrAddUnregistered(value);
            return true;
        }
    }

    /// <summary>
    ///   Turns text into a source, as <see cref="Parse(string, IFormatProvider?)"/>
    ///   does, without throwing.
    /// </summary>
    /// <param name="s">The text.</param>
    /// <param name="result">The source, if the text was valid.</param>
    /// <returns><c>true</c> if the text named a source.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, [MaybeNullWhen(false)] out MetadataSource result)
        => TryParse(s, null, out result);

    /// <summary>
    ///   Gets the source with exactly this value, registered or not, without
    ///   looking at aliases. For values read back from storage, which must
    ///   keep naming the source they were stored for.
    /// </summary>
    /// <param name="value">The value, as stored.</param>
    /// <returns>The registered source, or the unregistered one.</returns>
    /// <exception cref="ArgumentException">
    ///   <paramref name="value"/> is not a valid value.
    /// </exception>
    internal static MetadataSource GetByValue(string value)
    {
        if (!IsValidValue(value))
            throw new ArgumentException($"\"{value}\" is not a valid metadata source value.", nameof(value));

        if (_registeredByValue.TryGetValue(value, out var source))
            return source;

        lock (_registryLock)
            return _registeredByValue.TryGetValue(value, out source) ? source : GetOrAddUnregistered(value);
    }

    /// <summary>
    ///   Closes registration, once every plugin is set up. Lookups and parsing
    ///   work as before; <see cref="Register"/> throws from then on.
    /// </summary>
    internal static void Freeze()
    {
        lock (_registryLock)
            _frozen = true;
    }

    /// <summary>
    ///   Opens registration again. For tests only, which undo their own
    ///   <see cref="Freeze"/>.
    /// </summary>
    internal static void Unfreeze()
    {
        lock (_registryLock)
            _frozen = false;
    }

    /// <summary>
    ///   Turns text into the value it would name: trimmed, lowercased, and
    ///   with every <c>_</c> read as <c>-</c>. The result may still be invalid.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The candidate value.</returns>
    internal static string ToCandidateValue(string text)
        => MetadataKeyComparer.Normalize(text.Trim()).ToLowerInvariant();

    // Callers hold the registry lock.
    private static MetadataSource GetOrAddUnregistered(string value)
        => _unregisteredByValue.GetOrAdd(value, v => new(v, ToPascalCase(v), [], false, null, registered: false));

    private static MetadataSource RegisterCore(string name, string value, IReadOnlyList<string> aliases, bool local, string description)
        => RegisterInternal(name, value, aliases, local, description, core: true);

    private static MetadataSource RegisterInternal(string name, string value, IReadOnlyList<string> aliases, bool local, string? description, bool core)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        name = name.Trim();
        description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (!IsValidValue(value))
            throw new ArgumentException($"\"{value}\" is not a valid metadata source value; it must be lowercase kebab-case, at most 64 characters.", nameof(value));
        if (!IsValidName(name))
            throw new ArgumentException($"\"{name}\" is not a valid metadata source name; it must be at most 64 characters, with no whitespace but spaces.", nameof(name));

        var cleanAliases = aliases.Select(alias => alias?.Trim() ?? string.Empty).ToList();
        if (cleanAliases.FirstOrDefault(alias => !IsValidAlias(alias)) is { } badAlias)
            throw new ArgumentException($"\"{badAlias}\" is not a valid metadata source alias.", nameof(aliases));

        // The value is a key already, so an alias repeating it adds nothing.
        cleanAliases = cleanAliases
            .Where(alias => !MetadataKeyComparer.Instance.Equals(alias, value))
            .Distinct(MetadataKeyComparer.Instance)
            .ToList();

        MetadataSource source;
        bool merged;
        lock (_registryLock)
        {
            if (_frozen)
                throw new InvalidOperationException($"Can't register the metadata source \"{value}\": registration closed after plugin setup.");

            if (_registeredByValue.TryGetValue(value, out var existing))
            {
                var added = cleanAliases.Where(alias => !existing.Aliases.Contains(alias, MetadataKeyComparer.Instance)).ToList();
                var fillsDescription = existing.Description is null && description is not null;
                if (existing._core && (existing.IsLocal != local || added.Count > 0))
                    throw new ArgumentException($"\"{value}\" is a core metadata source and can't be changed.", nameof(value));
                if (existing.IsLocal != local)
                    throw new ArgumentException($"\"{value}\" is already registered as {(existing.IsLocal ? "local" : "remote")}.", nameof(local));
                if (added.Count is 0 && !fillsDescription)
                    return existing;

                EnsureFree(added, value);
                if (added.Count > 0)
                    existing.Aliases = [.. existing.Aliases, .. added];
                if (fillsDescription)
                    existing.Description = description;
                foreach (var alias in added)
                    _registeredByKey[alias] = existing;
                source = existing;
                merged = true;
            }
            else
            {
                var keys = new List<string> { value };
                keys.AddRange(cleanAliases);
                EnsureFree(keys, value);
                source = new(value, name, cleanAliases, local, description, registered: true) { _core = core };
                _registeredByValue[value] = source;
                foreach (var key in keys)
                    _registeredByKey[key] = source;
                Volatile.Write(ref _all, [.. _all, source]);
                merged = false;
            }
        }

        Registered?.Invoke(source, merged);
        return source;
    }

    /// <summary>
    ///   The words the API's <c>/api/v3/Metadata</c> routes use next to a
    ///   source's value, which no source may be registered under, since the
    ///   routes would never reach it.
    /// </summary>
    internal static readonly IReadOnlySet<string> ReservedKeys = new HashSet<string>(["provider", "entry", "episode"], MetadataKeyComparer.Instance);

    // Callers hold the registry lock.
    private static void EnsureFree(IEnumerable<string> keys, string value)
    {
        foreach (var key in keys)
        {
            if (ReservedKeys.Contains(key))
                throw new ArgumentException($"\"{key}\" is kept for the API's own metadata routes, so no source can be registered under it.");
            if (MetadataKeyComparer.Normalize(key).StartsWith(UnknownPrefix, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"\"{key}\" starts with \"{UnknownPrefix}\", which is kept for stored numbers that name no metadata source.");
            if (_registeredByKey.TryGetValue(key, out var owner) && owner.Value != value)
                throw new ArgumentException($"\"{key}\" is already taken by the metadata source \"{owner.Value}\".");
            if (!MetadataKeyComparer.Instance.Equals(key, value) && _unregisteredByValue.ContainsKey(ToCandidateValue(key)))
                throw new ArgumentException($"\"{key}\" is already in use as a metadata source of its own, so it can't become a key of \"{value}\".");
        }
    }

    /// <summary>
    ///   Checks whether text is a valid value: lowercase kebab-case, at most
    ///   <see cref="MaxValueLength"/> characters.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <returns><c>true</c> if the text is a valid value.</returns>
    internal static bool IsValidValue([NotNullWhen(true)] string? value)
        => value is { Length: > 0 and <= MaxValueLength } && ValueRegex().IsMatch(value);

    private static bool IsValidName(string name)
        => name is { Length: > 0 and <= MaxValueLength } && name.All(character => character is ' ' || !(char.IsWhiteSpace(character) || char.IsControl(character)));

    private static bool IsValidAlias([NotNullWhen(true)] string? alias)
        => alias is { Length: > 0 and <= MaxValueLength } && !alias.Any(char.IsWhiteSpace);

    private static string ToPascalCase(string value)
        => string.Concat(value.Split('-').Select(part => char.ToUpperInvariant(part[0]) + part[1..]));

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex ValueRegex();

    #endregion

    #region Instance

    /// <summary>
    ///   The most characters a value can have.
    /// </summary>
    public const int MaxValueLength = 64;

    private readonly bool _registered;

    private bool _core;

    private MetadataSource(string value, string name, IReadOnlyList<string> aliases, bool local, string? description, bool registered)
    {
        Value = value;
        Name = name;
        Aliases = aliases;
        IsLocal = local;
        Description = description;
        _registered = registered;
    }

    /// <summary>
    ///   The kebab-case value, e.g. <c>anidb</c>. It is what saved settings
    ///   and stored JSON hold, and what <see cref="ToString"/> returns.
    /// </summary>
    [RegularExpression("^[a-z0-9]+(-[a-z0-9]+)*$"), MaxLength(MaxValueLength)]
    public string Value { get; }

    /// <summary>
    ///   The display name, e.g. <c>AniDB</c> or <c>Locally Generated</c>. Only
    ///   for showing: it may hold spaces, need not be unique, and is never
    ///   accepted as input. An unregistered source is named after its value
    ///   in PascalCase.
    /// </summary>
    public string Name
    {
        get => !_registered && _registeredByValue.TryGetValue(Value, out var registered) ? registered.Name : field;
        internal set;
    }

    /// <summary>
    ///   Other spellings accepted as input besides the value, ignoring case
    ///   and reading <c>_</c> as <c>-</c>.
    /// </summary>
    public IReadOnlyList<string> Aliases
    {
        get => !_registered && _registeredByValue.TryGetValue(Value, out var registered) ? registered.Aliases : field;
        internal set;
    }

    /// <summary>
    ///   A short, plain description of the source, for display, or
    ///   <c>null</c> if none was given. An unregistered source has none until
    ///   its value is registered.
    /// </summary>
    public string? Description
    {
        get => !_registered && _registeredByValue.TryGetValue(Value, out var registered) ? registered.Description : field;
        internal set;
    }

    /// <summary>
    ///   Whether the source's data is made on this server rather than fetched
    ///   from a remote service.
    /// </summary>
    public bool IsLocal
    {
        get => !_registered && _registeredByValue.TryGetValue(Value, out var registered) ? registered.IsLocal : field;
        internal set;
    }

    /// <summary>
    ///   Whether the source's data is fetched from a remote service.
    /// </summary>
    public bool IsRemote => !IsLocal;

    /// <summary>
    ///   Whether the source is registered, by the core or a plugin.
    /// </summary>
    public bool IsRegistered => _registered || _registeredByValue.ContainsKey(Value);

    /// <summary>
    ///   Whether the source is one the core registers and answers for itself:
    ///   <see cref="Shoko"/>, <see cref="User"/>, <see cref="Generated"/> or
    ///   <see cref="AniDB"/>. <see cref="TMDB"/> is registered up front but
    ///   kept in the shared stores, like a plugin's source.
    /// </summary>
    internal bool IsCore => _core || (!_registered && _registeredByValue.TryGetValue(Value, out var registered) && registered._core);

    #endregion

    #region Equality

    /// <inheritdoc/>
    public bool Equals(MetadataSource? other)
        => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is MetadataSource other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
        => StringComparer.Ordinal.GetHashCode(Value);

    /// <summary>
    ///   Checks whether two sources have the same value.
    /// </summary>
    /// <param name="left">The first source.</param>
    /// <param name="right">The second source.</param>
    /// <returns><c>true</c> if both are <c>null</c> or have the same value.</returns>
    public static bool operator ==(MetadataSource? left, MetadataSource? right)
        => left is null ? right is null : left.Equals(right);

    /// <summary>
    ///   Checks whether two sources have different values.
    /// </summary>
    /// <param name="left">The first source.</param>
    /// <param name="right">The second source.</param>
    /// <returns><c>true</c> if the values differ.</returns>
    public static bool operator !=(MetadataSource? left, MetadataSource? right)
        => !(left == right);

    /// <summary>
    ///   Compares two sources in the old enum's order: AniDB, TMDB and the
    ///   other old remote sources, then every newer source, then the plugin
    ///   and local sources. Sources of the same rank go by value, ordinally.
    /// </summary>
    /// <param name="other">The source to compare with.</param>
    /// <returns>Less than zero, zero or more than zero, as for strings.</returns>
    public int CompareTo(MetadataSource? other)
    {
        if (other is null)
            return 1;

        var byRank = GetSortRank(Value).CompareTo(GetSortRank(other.Value));
        return byRank is not 0 ? byRank : string.CompareOrdinal(Value, other.Value);
    }

    /// <summary>
    ///   Compares with another source, as <see cref="CompareTo(MetadataSource?)"/>
    ///   does.
    /// </summary>
    /// <param name="obj">The source to compare with, or <c>null</c>.</param>
    /// <returns>Less than zero, zero or more than zero, as for strings.</returns>
    /// <exception cref="ArgumentException">
    ///   <paramref name="obj"/> is neither <c>null</c> nor a source.
    /// </exception>
    int IComparable.CompareTo(object? obj)
        => obj is null or MetadataSource ? CompareTo(obj as MetadataSource) : throw new ArgumentException("Can only compare with another metadata source.", nameof(obj));

    private static int GetSortRank(string value)
        => _sortRanks.TryGetValue(value, out var rank) ? rank : NewValueRank;

    /// <summary>
    ///   Gets the kebab-case value.
    /// </summary>
    /// <returns>The <see cref="Value"/>.</returns>
    public override string ToString()
        => Value;

    #endregion
}
