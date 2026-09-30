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
///   A kind of entity, such as a series, an episode or a plugin's own kind,
///   named by a kebab-case <see cref="Value"/>. Kinds are registered up front
///   and shared; text is turned into a kind with <see cref="Get"/>,
///   <see cref="TryGet"/> or <see cref="Parse(string, IFormatProvider?)"/>,
///   which accept a value or an alias, ignoring case and reading <c>_</c> as
///   <c>-</c>.
/// </summary>
/// <remarks>
///   Registration closes once every plugin is set up. A plugin registers its
///   kinds from the static constructor of a class it touches in
///   <c>IPluginServiceRegistration.RegisterServices</c> (<c>IPlugin.Setup</c>
///   at the latest). A kind another plugin owns is looked up with
///   <see cref="TryGet"/> on every access, never cached. Kinds chosen by
///   settings apply on the next start (<see cref="Core.Services.ISystemService.RequireRestart{TPlugin}(string)"/>).
/// </remarks>
[TypeConverter(typeof(MetadataEntityTypeTypeConverter))]
[System.Text.Json.Serialization.JsonConverter(typeof(MetadataEntityTypeJsonConverter))]
[Newtonsoft.Json.JsonConverter(typeof(MetadataEntityTypeNewtonsoftJsonConverter))]
public sealed partial class MetadataEntityType : IEquatable<MetadataEntityType>, IComparable<MetadataEntityType>, IComparable, IParsable<MetadataEntityType>
{
    #region Sort Order

    /// <summary>
    ///   The sort rank of every value the old enum didn't have: after all of
    ///   its members.
    /// </summary>
    private const int NewValueRank = 0x0D;

    /// <summary>
    ///   The sort rank of each value the old enum had, which is the number
    ///   the enum gave it, so kinds keep sorting in the order they did.
    /// </summary>
    private static readonly FrozenDictionary<string, byte> _sortRanks = new Dictionary<string, byte>
    {
        ["collection"] = 0x01,
        ["series"] = 0x02,
        ["season"] = 0x03,
        ["episode"] = 0x04,
        ["movie"] = 0x05,
        ["video"] = 0x06,
        ["studio"] = 0x07,
        ["network"] = 0x08,
        ["creator"] = 0x09,
        ["character"] = 0x0A,
        ["user"] = 0x0B,
        ["library"] = 0x0C,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    #endregion

    #region Registry

    /// <summary>
    ///   The start of the value a stored number that names no kind reads back
    ///   as, e.g. <c>unknown-200</c>. No registered key may start with it.
    /// </summary>
    internal const string UnknownPrefix = "unknown-";

    private static readonly Lock _registryLock = new();

    private static readonly ConcurrentDictionary<string, MetadataEntityType> _registeredByValue = new(StringComparer.Ordinal);

    /// <summary>
    ///   Every registered kind under each of its keys: its value and its
    ///   aliases, ignoring case and reading <c>_</c> as <c>-</c>.
    /// </summary>
    private static readonly ConcurrentDictionary<string, MetadataEntityType> _registeredByKey = new(MetadataKeyComparer.Instance);

    private static readonly ConcurrentDictionary<string, MetadataEntityType> _unregisteredByValue = new(StringComparer.Ordinal);

    private static MetadataEntityType[] _all = [];

    private static volatile bool _frozen;

    /// <summary>
    ///   Raised after a kind is registered, or after a registration merged new
    ///   aliases or a description into an existing kind. The flag tells which.
    /// </summary>
    internal static event Action<MetadataEntityType, bool>? Registered;

    static MetadataEntityType()
    {
        Series = RegisterCore("Series", "series", ["anime", "show"], "An anime or a show, made up of episodes.");
        Season = RegisterCore("Season", "season", [], "A season of a series, holding some of its episodes.");
        Episode = RegisterCore("Episode", "episode", [], "A single episode of a series.");
        Movie = RegisterCore("Movie", "movie", ["film"], "A movie, on its own or as part of a series.");
        Collection = RegisterCore("Collection", "collection", ["group", "boxset", "franchise"], "A collection holding other entries, such as a group of series.");
        Studio = RegisterCore("Studio", "studio", ["company"], "A studio or company that worked on something.");
        Network = RegisterCore("Network", "network", [], "A network that airs or streams something.");
        Channel = RegisterCore("Channel", "channel", [], "A channel something airs on, from the airing schedules.");
        Creator = RegisterCore("Creator", "creator", ["person", "staffmember"], "A person or staff member who worked on something.");
        Character = RegisterCore("Character", "character", [], "A character in a work.");
        Tag = RegisterCore("Tag", "tag", [], "A tag describing an entry.");
        Filter = RegisterCore("Filter", "filter", [], "A filter that picks out entries from the collection.");
        Video = RegisterCore("Video", "video", [], "A video file the server keeps track of.");
        User = RegisterCore("User", "user", [], "A user of the server.");
        Ordering = RegisterCore("Ordering", "ordering", [], "Another way to group a series' episodes, such as a DVD order.");
    }

    /// <summary>
    ///   A series, such as an anime or a show. Also read from <c>anime</c> and
    ///   <c>show</c>.
    /// </summary>
    public static MetadataEntityType Series { get; }

    /// <summary>
    ///   A season within a series.
    /// </summary>
    public static MetadataEntityType Season { get; }

    /// <summary>
    ///   An episode within a season or a series.
    /// </summary>
    public static MetadataEntityType Episode { get; }

    /// <summary>
    ///   A movie.
    /// </summary>
    public static MetadataEntityType Movie { get; }

    /// <summary>
    ///   A collection that holds other entries, such as a Shoko group. Also
    ///   read from <c>group</c>, <c>boxset</c> and <c>franchise</c>.
    /// </summary>
    public static MetadataEntityType Collection { get; }

    /// <summary>
    ///   A studio or company that worked on something. Also read from
    ///   <c>company</c>.
    /// </summary>
    public static MetadataEntityType Studio { get; }

    /// <summary>
    ///   A network that airs or streams something.
    /// </summary>
    public static MetadataEntityType Network { get; }

    /// <summary>
    ///   A channel something airs on, from the airing schedules.
    /// </summary>
    public static MetadataEntityType Channel { get; }

    /// <summary>
    ///   A creator, such as a person or a staff member. Also read from
    ///   <c>person</c> and <c>staffmember</c>.
    /// </summary>
    public static MetadataEntityType Creator { get; }

    /// <summary>
    ///   A character within a work.
    /// </summary>
    public static MetadataEntityType Character { get; }

    /// <summary>
    ///   A tag.
    /// </summary>
    public static MetadataEntityType Tag { get; }

    /// <summary>
    ///   A filter.
    /// </summary>
    public static MetadataEntityType Filter { get; }

    /// <summary>
    ///   A video file.
    /// </summary>
    public static MetadataEntityType Video { get; }

    /// <summary>
    ///   A user.
    /// </summary>
    public static MetadataEntityType User { get; }

    /// <summary>
    ///   An ordering of a series' episodes into groups, such as a DVD order,
    ///   next to the series' own seasons.
    /// </summary>
    public static MetadataEntityType Ordering { get; }

    /// <summary>
    ///   Every registered kind, the core's and the plugins', in the order they
    ///   were registered.
    /// </summary>
    public static IReadOnlyList<MetadataEntityType> All => Volatile.Read(ref _all);

    /// <summary>
    ///   Whether registration has closed, which happens once every plugin is
    ///   set up.
    /// </summary>
    internal static bool IsFrozen => _frozen;

    /// <summary>
    ///   Registers a kind, or merges into the kind already registered under
    ///   <paramref name="value"/>, and returns its one instance. A merge adds
    ///   the new aliases, keeps the first name, and keeps the first
    ///   description unless it was empty. Closes once every plugin is set up.
    /// </summary>
    /// <remarks>
    ///   Call it from the static constructor of a class holding the plugin's
    ///   own kinds, and touch that class from
    ///   <c>IPluginServiceRegistration.RegisterServices</c>. A kind another
    ///   plugin registers is looked up with <see cref="TryGet"/> on every
    ///   access instead; see the remarks on <see cref="MetadataEntityType"/>.
    /// </remarks>
    /// <param name="name">
    ///   The display name, e.g. <c>Media Library</c>. Only shown, never
    ///   accepted as input; it may hold spaces and need not be unique. At most
    ///   64 characters.
    /// </param>
    /// <param name="value">
    ///   The kebab-case value, e.g. <c>media-library</c>. At most 64 characters.
    /// </param>
    /// <param name="aliases">
    ///   Other spellings accepted as input, e.g. <c>library</c>. No whitespace.
    /// </param>
    /// <param name="description">
    ///   A short, plain description of the kind, for display. Optional.
    /// </param>
    /// <returns>The registered kind.</returns>
    /// <exception cref="ArgumentException">
    ///   The name, value or an alias is invalid; the value or an alias is
    ///   another kind's value or alias, was already used as a kind of its own,
    ///   or starts with <c>unknown-</c>; or the registration would change a
    ///   core kind.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Registration closed after plugin setup. A kind that depends on a
    ///   setting changed later is registered on the next start; raise a
    ///   restart reason with
    ///   <see cref="Core.Services.ISystemService.RequireRestart{TPlugin}(string)"/> instead.
    /// </exception>
    public static MetadataEntityType Register(string name, string value, IReadOnlyList<string>? aliases = null, string? description = null)
        => RegisterInternal(name, value, aliases ?? [], description, core: false);

    /// <summary>
    ///   Finds a registered kind by its value or an alias, ignoring case and
    ///   reading <c>_</c> as <c>-</c>.
    /// </summary>
    /// <param name="text">The value or alias.</param>
    /// <param name="entityType">The registered kind, if found.</param>
    /// <returns><c>true</c> if a registered kind was found.</returns>
    public static bool TryGet([NotNullWhen(true)] string? text, [NotNullWhen(true)] out MetadataEntityType? entityType)
    {
        entityType = null;
        return !string.IsNullOrWhiteSpace(text) && _registeredByKey.TryGetValue(text.Trim(), out entityType);
    }

    /// <summary>
    ///   Gets a registered kind by its value or an alias, ignoring case and
    ///   reading <c>_</c> as <c>-</c>.
    /// </summary>
    /// <param name="text">The value or alias.</param>
    /// <returns>The registered kind.</returns>
    /// <exception cref="KeyNotFoundException">
    ///   No kind is registered under <paramref name="text"/>.
    /// </exception>
    public static MetadataEntityType Get(string text)
        => TryGet(text, out var entityType) ? entityType : throw new KeyNotFoundException($"No metadata entity type is registered as \"{text}\".");

    /// <summary>
    ///   Turns text into a kind: a registered kind's value or alias gives that
    ///   kind, ignoring case and reading <c>_</c> as <c>-</c>, and any other
    ///   text that is a valid value once lowercased and hyphenated gives an
    ///   unregistered kind.
    /// </summary>
    /// <param name="s">The text.</param>
    /// <param name="provider">Ignored.</param>
    /// <returns>The kind.</returns>
    /// <exception cref="FormatException">
    ///   <paramref name="s"/> is neither known nor a valid value.
    /// </exception>
    public static MetadataEntityType Parse(string s, IFormatProvider? provider = null)
        => TryParse(s, provider, out var entityType) ? entityType : throw new FormatException($"\"{s}\" is not a valid metadata entity type.");

    /// <summary>
    ///   Turns text into a kind, as <see cref="Parse(string, IFormatProvider?)"/>
    ///   does, without throwing.
    /// </summary>
    /// <param name="s">The text.</param>
    /// <param name="provider">Ignored.</param>
    /// <param name="result">The kind, if the text was valid.</param>
    /// <returns><c>true</c> if the text named a kind.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out MetadataEntityType result)
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
        // meanwhile. A value handed out here can no longer become another kind's alias.
        lock (_registryLock)
        {
            result = _registeredByKey.TryGetValue(value, out var registered) ? registered : GetOrAddUnregistered(value);
            return true;
        }
    }

    /// <summary>
    ///   Turns text into a kind, as <see cref="Parse(string, IFormatProvider?)"/>
    ///   does, without throwing.
    /// </summary>
    /// <param name="s">The text.</param>
    /// <param name="result">The kind, if the text was valid.</param>
    /// <returns><c>true</c> if the text named a kind.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, [MaybeNullWhen(false)] out MetadataEntityType result)
        => TryParse(s, null, out result);

    /// <summary>
    ///   Gets the kind with exactly this value, registered or not, without
    ///   looking at aliases. For values read back from storage, which must
    ///   keep naming the kind they were stored for.
    /// </summary>
    /// <param name="value">The value, as stored.</param>
    /// <returns>The registered kind, or the unregistered one.</returns>
    /// <exception cref="ArgumentException">
    ///   <paramref name="value"/> is not a valid value.
    /// </exception>
    internal static MetadataEntityType GetByValue(string value)
    {
        if (!IsValidValue(value))
            throw new ArgumentException($"\"{value}\" is not a valid metadata entity type value.", nameof(value));

        if (_registeredByValue.TryGetValue(value, out var entityType))
            return entityType;

        lock (_registryLock)
            return _registeredByValue.TryGetValue(value, out entityType) ? entityType : GetOrAddUnregistered(value);
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
    private static MetadataEntityType GetOrAddUnregistered(string value)
        => _unregisteredByValue.GetOrAdd(value, v => new(v, ToPascalCase(v), [], null, registered: false));

    private static MetadataEntityType RegisterCore(string name, string value, IReadOnlyList<string> aliases, string description)
        => RegisterInternal(name, value, aliases, description, core: true);

    private static MetadataEntityType RegisterInternal(string name, string value, IReadOnlyList<string> aliases, string? description, bool core)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        name = name.Trim();
        description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (!IsValidValue(value))
            throw new ArgumentException($"\"{value}\" is not a valid metadata entity type value; it must be lowercase kebab-case, at most 64 characters.", nameof(value));
        if (!IsValidName(name))
            throw new ArgumentException($"\"{name}\" is not a valid metadata entity type name; it must be at most 64 characters, with no whitespace but spaces.", nameof(name));

        var cleanAliases = aliases.Select(alias => alias?.Trim() ?? string.Empty).ToList();
        if (cleanAliases.FirstOrDefault(alias => !IsValidAlias(alias)) is { } badAlias)
            throw new ArgumentException($"\"{badAlias}\" is not a valid metadata entity type alias.", nameof(aliases));

        // The value is a key already, so an alias repeating it adds nothing.
        cleanAliases = cleanAliases
            .Where(alias => !MetadataKeyComparer.Instance.Equals(alias, value))
            .Distinct(MetadataKeyComparer.Instance)
            .ToList();

        MetadataEntityType entityType;
        bool merged;
        lock (_registryLock)
        {
            if (_frozen)
                throw new InvalidOperationException($"Can't register the metadata entity type \"{value}\": registration closed after plugin setup.");

            if (_registeredByValue.TryGetValue(value, out var existing))
            {
                var added = cleanAliases.Where(alias => !existing.Aliases.Contains(alias, MetadataKeyComparer.Instance)).ToList();
                var fillsDescription = existing.Description is null && description is not null;
                if (existing._core && added.Count > 0)
                    throw new ArgumentException($"\"{value}\" is a core metadata entity type and can't be changed.", nameof(value));
                if (added.Count is 0 && !fillsDescription)
                    return existing;

                EnsureFree(added, value);
                if (added.Count > 0)
                    existing.Aliases = [.. existing.Aliases, .. added];
                if (fillsDescription)
                    existing.Description = description;
                foreach (var alias in added)
                    _registeredByKey[alias] = existing;
                entityType = existing;
                merged = true;
            }
            else
            {
                var keys = new List<string> { value };
                keys.AddRange(cleanAliases);
                EnsureFree(keys, value);
                entityType = new(value, name, cleanAliases, description, registered: true) { _core = core };
                _registeredByValue[value] = entityType;
                foreach (var key in keys)
                    _registeredByKey[key] = entityType;
                Volatile.Write(ref _all, [.. _all, entityType]);
                merged = false;
            }
        }

        Registered?.Invoke(entityType, merged);
        return entityType;
    }

    // Callers hold the registry lock.
    private static void EnsureFree(IEnumerable<string> keys, string value)
    {
        foreach (var key in keys)
        {
            if (MetadataKeyComparer.Normalize(key).StartsWith(UnknownPrefix, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"\"{key}\" starts with \"{UnknownPrefix}\", which is kept for stored numbers that name no metadata entity type.");
            if (_registeredByKey.TryGetValue(key, out var owner) && owner.Value != value)
                throw new ArgumentException($"\"{key}\" is already taken by the metadata entity type \"{owner.Value}\".");
            if (!MetadataKeyComparer.Instance.Equals(key, value) && _unregisteredByValue.ContainsKey(ToCandidateValue(key)))
                throw new ArgumentException($"\"{key}\" is already in use as a metadata entity type of its own, so it can't become a key of \"{value}\".");
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

    private MetadataEntityType(string value, string name, IReadOnlyList<string> aliases, string? description, bool registered)
    {
        Value = value;
        Name = name;
        Aliases = aliases;
        Description = description;
        _registered = registered;
    }

    /// <summary>
    ///   The kebab-case value, e.g. <c>series</c>. It is what saved settings
    ///   and stored JSON hold, and what <see cref="ToString"/> returns.
    /// </summary>
    [RegularExpression("^[a-z0-9]+(-[a-z0-9]+)*$"), MaxLength(MaxValueLength)]
    public string Value { get; }

    /// <summary>
    ///   The display name, e.g. <c>Series</c>. Only for showing: it may hold
    ///   spaces, need not be unique, and is never accepted as input. An
    ///   unregistered kind is named after its value in PascalCase.
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
    ///   A short, plain description of the kind, for display, or <c>null</c>
    ///   if none was given. An unregistered kind has none until its value is
    ///   registered.
    /// </summary>
    public string? Description
    {
        get => !_registered && _registeredByValue.TryGetValue(Value, out var registered) ? registered.Description : field;
        internal set;
    }

    /// <summary>
    ///   Whether the kind is registered, by the core or a plugin.
    /// </summary>
    public bool IsRegistered => _registered || _registeredByValue.ContainsKey(Value);

    #endregion

    #region Equality

    /// <inheritdoc/>
    public bool Equals(MetadataEntityType? other)
        => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is MetadataEntityType other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
        => StringComparer.Ordinal.GetHashCode(Value);

    /// <summary>
    ///   Checks whether two kinds have the same value.
    /// </summary>
    /// <param name="left">The first kind.</param>
    /// <param name="right">The second kind.</param>
    /// <returns><c>true</c> if both are <c>null</c> or have the same value.</returns>
    public static bool operator ==(MetadataEntityType? left, MetadataEntityType? right)
        => left is null ? right is null : left.Equals(right);

    /// <summary>
    ///   Checks whether two kinds have different values.
    /// </summary>
    /// <param name="left">The first kind.</param>
    /// <param name="right">The second kind.</param>
    /// <returns><c>true</c> if the values differ.</returns>
    public static bool operator !=(MetadataEntityType? left, MetadataEntityType? right)
        => !(left == right);

    /// <summary>
    ///   Compares two kinds in the old enum's order, from collection to
    ///   library, then every newer kind. Kinds of the same rank go by value,
    ///   ordinally.
    /// </summary>
    /// <param name="other">The kind to compare with.</param>
    /// <returns>Less than zero, zero or more than zero, as for strings.</returns>
    public int CompareTo(MetadataEntityType? other)
    {
        if (other is null)
            return 1;

        var byRank = GetSortRank(Value).CompareTo(GetSortRank(other.Value));
        return byRank is not 0 ? byRank : string.CompareOrdinal(Value, other.Value);
    }

    /// <summary>
    ///   Compares with another kind, as <see cref="CompareTo(MetadataEntityType?)"/>
    ///   does.
    /// </summary>
    /// <param name="obj">The kind to compare with, or <c>null</c>.</param>
    /// <returns>Less than zero, zero or more than zero, as for strings.</returns>
    /// <exception cref="ArgumentException">
    ///   <paramref name="obj"/> is neither <c>null</c> nor a kind.
    /// </exception>
    int IComparable.CompareTo(object? obj)
        => obj is null or MetadataEntityType ? CompareTo(obj as MetadataEntityType) : throw new ArgumentException("Can only compare with another metadata entity type.", nameof(obj));

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
