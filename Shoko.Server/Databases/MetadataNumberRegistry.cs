using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using NLog;
using Shoko.Abstractions.Metadata;

namespace Shoko.Server.Databases;

/// <summary>
/// Maps each <see cref="MetadataSource"/> and <see cref="MetadataEntityType"/>
/// to the number its database columns store. The numbers never leave the
/// persistence layer.
/// </summary>
/// <remarks>
/// The old enum values and core entity types have fixed numbers. Any other
/// registered source gets the next free one when first stored, an entity type
/// when it registers, both kept in <c>metadata-registry.json</c> in the data
/// folder. An unregistered one never gets a new number. The columns are one
/// byte wide.
/// </remarks>
public static class MetadataNumberRegistry
{
    #region Fields

    /// <summary>
    /// The file name, in the data folder.
    /// </summary>
    public const string FileName = "metadata-registry.json";

    /// <summary>
    /// How few free numbers are left before a warning is logged.
    /// </summary>
    internal const int LowFreeNumberCount = 32;

    private const byte FirstFreeSourceNumber = 0x0E;

    private const byte LastFreeSourceNumber = 0xFA;

    private const byte FirstFreeEntityTypeNumber = 0x11;

    /// <summary>
    /// The last number an entity type is handed. MySQL keeps
    /// <c>ShokoImage_Entity.EntityType</c> as a signed byte, so the numbers
    /// stop at 127.
    /// </summary>
    private const byte LastFreeEntityTypeNumber = 0x7F;

    private const string UnknownPrefix = MetadataSource.UnknownPrefix;

    private const string UnknownEntityTypePrefix = MetadataEntityType.UnknownPrefix;

    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    private static readonly Lock _lock = new();

    /// <summary>
    /// The old enum's numbers, fixed for good, by the value that replaced
    /// each member.
    /// </summary>
    private static readonly FrozenDictionary<string, byte> _fixedSourceNumbers = new Dictionary<string, byte>
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

    /// <summary>
    /// The old enum's numbers, fixed for good, by number.
    /// </summary>
    private static readonly FrozenDictionary<byte, string> _fixedSources = _fixedSourceNumbers
        .ToFrozenDictionary(pair => pair.Value, pair => pair.Key);

    /// <summary>
    /// The old enum's numbers, fixed for good, by value.
    /// </summary>
    internal static IReadOnlyDictionary<string, byte> FixedSourceNumbers => _fixedSourceNumbers;

    private static readonly Dictionary<string, byte> _assignedSources = new(StringComparer.Ordinal);

    private static readonly Dictionary<byte, string> _assignedSourceValues = [];

    /// <summary>
    /// The old entity type enum's numbers, and those of the core entity types
    /// added since, fixed for good, by value. <c>library</c> left the core but
    /// keeps its number, for the plugin that registers it.
    /// </summary>
    private static readonly FrozenDictionary<string, byte> _fixedEntityTypeNumbers = new Dictionary<string, byte>
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
        ["tag"] = 0x0D,
        ["filter"] = 0x0E,
        ["channel"] = 0x0F,
        ["ordering"] = 0x10,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// The fixed entity type numbers, by number.
    /// </summary>
    private static readonly FrozenDictionary<byte, string> _fixedEntityTypes = _fixedEntityTypeNumbers
        .ToFrozenDictionary(pair => pair.Value, pair => pair.Key);

    /// <summary>
    /// The fixed entity type numbers, by value.
    /// </summary>
    internal static IReadOnlyDictionary<string, byte> FixedEntityTypeNumbers => _fixedEntityTypeNumbers;

    private static readonly Dictionary<string, byte> _assignedEntityTypes = new(StringComparer.Ordinal);

    private static readonly Dictionary<byte, string> _assignedEntityTypeValues = [];

    private static string? _filePath;

    /// <summary>
    /// Set when the file couldn't be read. It's left alone, and no new number
    /// is handed out, since any free number may be one the lost file gave to
    /// a source whose rows are still stored.
    /// </summary>
    private static bool _unreadable;

    private static bool _warnedAboutFreeNumbers;

    private static bool _warnedAboutFreeEntityTypeNumbers;

    #endregion

    #region Setup

    /// <summary>
    /// Loads the assigned numbers from the data folder and starts handing out
    /// numbers to sources and entity types as they register. Called once,
    /// first thing on startup, before any plugin loads.
    /// </summary>
    /// <param name="dataPath">The data folder.</param>
    public static void Load(string dataPath)
    {
        lock (_lock)
        {
            _filePath = Path.Combine(dataPath, FileName);
            _unreadable = false;
            _warnedAboutFreeNumbers = false;
            _warnedAboutFreeEntityTypeNumbers = false;
            _assignedSources.Clear();
            _assignedSourceValues.Clear();
            _assignedEntityTypes.Clear();
            _assignedEntityTypeValues.Clear();
            if (File.Exists(_filePath))
            {
                try
                {
                    // An empty file or a null reads as null rather than failing, but is as broken as any other.
                    var file = JsonConvert.DeserializeObject<RegistryFile>(File.ReadAllText(_filePath))
                        ?? throw new JsonSerializationException("The file is empty or null.");
                    if (file.Sources is null)
                        throw new JsonSerializationException("The file has no sources.");
                    if (file.EntityTypes is null)
                        throw new JsonSerializationException("The file has no entity types.");

                    foreach (var (value, number) in file.Sources)
                    {
                        if (!MetadataSource.IsValidValue(value) || number is < FirstFreeSourceNumber or > LastFreeSourceNumber)
                        {
                            _logger.Warn("Ignoring the metadata source {Value} in {File}; the value is invalid or its number {Number} is outside the free range.", value, FileName, number);
                            continue;
                        }

                        if (_assignedSourceValues.ContainsKey(number) || _fixedSourceNumbers.ContainsKey(value))
                        {
                            _logger.Warn("Ignoring the metadata source {Value} in {File}; its number {Number} is already taken.", value, FileName, number);
                            continue;
                        }

                        _assignedSources[value] = number;
                        _assignedSourceValues[number] = value;
                    }

                    foreach (var (value, number) in file.EntityTypes)
                    {
                        if (!MetadataEntityType.IsValidValue(value) || number is < FirstFreeEntityTypeNumber or > LastFreeEntityTypeNumber)
                        {
                            _logger.Warn("Ignoring the metadata entity type {Value} in {File}; the value is invalid or its number {Number} is outside the free range.", value, FileName, number);
                            continue;
                        }

                        if (_assignedEntityTypeValues.ContainsKey(number) || _fixedEntityTypeNumbers.ContainsKey(value))
                        {
                            _logger.Warn("Ignoring the metadata entity type {Value} in {File}; its number {Number} is already taken.", value, FileName, number);
                            continue;
                        }

                        _assignedEntityTypes[value] = number;
                        _assignedEntityTypeValues[number] = value;
                    }
                }
                catch (Exception ex)
                {
                    KeepUnreadableFile(_filePath, ex);
                }
            }

            WarnIfRunningLow();
        }

        MetadataSource.Registered -= OnRegistered;
        MetadataSource.Registered += OnRegistered;
        foreach (var source in MetadataSource.All)
            AssignNumber(source);

        MetadataEntityType.Registered -= OnEntityTypeRegistered;
        MetadataEntityType.Registered += OnEntityTypeRegistered;
        foreach (var entityType in MetadataEntityType.All)
            AssignNumber(entityType);
    }

    private static void OnRegistered(MetadataSource source, bool merged)
    {
        if (merged)
            _logger.Debug("Merged a registration into the metadata source {Value}; its aliases are now {Aliases}.", source.Value, source.Aliases);
        AssignNumber(source);
    }

    // One that can't have a number stays registered; storing its rows fails instead.
    private static void AssignNumber(MetadataSource source)
    {
        try
        {
            GetNumber(source);
        }
        catch (InvalidOperationException ex)
        {
            _logger.Error("Unable to give the metadata source {Value} a number, so its rows can't be stored: {Reason}", source.Value, ex.Message);
        }
    }

    private static void OnEntityTypeRegistered(MetadataEntityType entityType, bool merged)
    {
        if (merged)
            _logger.Debug("Merged a registration into the metadata entity type {Value}; its aliases are now {Aliases}.", entityType.Value, entityType.Aliases);
        else
            AssignNumber(entityType);
    }

    // The only place an entity type number is handed out. One that can't have a number
    // stays registered; storing its rows fails instead.
    private static void AssignNumber(MetadataEntityType entityType)
    {
        if (_fixedEntityTypeNumbers.ContainsKey(entityType.Value))
            return;

        lock (_lock)
        {
            if (_assignedEntityTypes.ContainsKey(entityType.Value))
                return;

            if (_unreadable)
            {
                _logger.Error("Unable to give the metadata entity type {Value} a number while {File} can't be read, so its rows can't be stored.", entityType.Value, FileName);
                return;
            }

            var number = Enumerable.Range(FirstFreeEntityTypeNumber, LastFreeEntityTypeNumber - FirstFreeEntityTypeNumber + 1)
                .Select(candidate => (byte)candidate)
                .FirstOrDefault(candidate => !_assignedEntityTypeValues.ContainsKey(candidate));
            if (number is 0)
            {
                _logger.Error("No free number is left for the metadata entity type {Value}, so its rows can't be stored.", entityType.Value);
                return;
            }

            _assignedEntityTypes[entityType.Value] = number;
            _assignedEntityTypeValues[number] = entityType.Value;
            Save();
            WarnIfRunningLow();
        }
    }

    // Skips the copy when an earlier one holds the same bytes. Callers hold the lock.
    private static void KeepUnreadableFile(string filePath, Exception ex)
    {
        _unreadable = true;
        const string advice = "No new metadata source or entity type gets a number until it's fixed; restore it from the copy kept next to the latest database backup, "
            + "or remove it to start numbering afresh, at the risk of rows stored under the lost numbers reading as other sources or entity types.";
        try
        {
            var content = File.ReadAllBytes(filePath);
            var copyPath = filePath + ".bad";
            for (var attempt = 1; File.Exists(copyPath); attempt++)
            {
                if (File.ReadAllBytes(copyPath).AsSpan().SequenceEqual(content))
                {
                    _logger.Error(ex, "Unable to read {File}, already kept as {Copy}. " + advice, FileName, copyPath);
                    return;
                }

                copyPath = $"{filePath}.{attempt}.bad";
            }

            File.Copy(filePath, copyPath, overwrite: false);
            _logger.Error(ex, "Unable to read {File}; kept a copy of it as {Copy}. " + advice, FileName, copyPath);
        }
        catch (Exception copyException)
        {
            _logger.Error(ex, "Unable to read {File}, and unable to keep a copy of it ({Reason}). " + advice, FileName, copyException.Message);
        }
    }

    #endregion

    #region Lookups

    /// <summary>
    /// Gets the number a source is stored as. A registered source without one
    /// gets the next free number; an unregistered source only has the number
    /// it was given before, or the one its <c>unknown-</c> value names if no
    /// source has that number.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The stored number.</returns>
    /// <exception cref="InvalidOperationException">
    /// The source is unregistered and has no number, or it needs a new number
    /// while the file can't be read or every free number is taken.
    /// </exception>
    public static byte GetNumber(MetadataSource source)
    {
        if (_fixedSourceNumbers.TryGetValue(source.Value, out var number))
            return number;

        lock (_lock)
        {
            if (_assignedSources.TryGetValue(source.Value, out number))
                return number;

            // Only a number that names no source, or the row would move to the source that has it.
            if (TryGetUnknownNumber(source.Value, UnknownPrefix, out number) && !_fixedSources.ContainsKey(number) && !_assignedSourceValues.ContainsKey(number))
                return number;

            if (!source.IsRegistered)
                throw new InvalidOperationException($"The metadata source \"{source.Value}\" is not registered and has no number, so it can't be stored.");

            if (_unreadable)
                throw new InvalidOperationException($"The metadata source \"{source.Value}\" has no number, and none is handed out while {FileName} can't be read.");

            number = Enumerable.Range(FirstFreeSourceNumber, LastFreeSourceNumber - FirstFreeSourceNumber + 1)
                .Select(candidate => (byte)candidate)
                .FirstOrDefault(candidate => !_assignedSourceValues.ContainsKey(candidate));
            if (number is 0)
                throw new InvalidOperationException($"No free number is left for the metadata source \"{source.Value}\".");

            _assignedSources[source.Value] = number;
            _assignedSourceValues[number] = source.Value;
            Save();
            WarnIfRunningLow();
            return number;
        }
    }

    /// <summary>
    /// Gets the number a source is already stored as, without handing out a
    /// new one, for a read that must never write the file or fail. A source
    /// without a number has no stored rows.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="number">The stored number, if the source has one.</param>
    /// <returns><c>true</c> if the source has a number.</returns>
    public static bool TryGetNumber(MetadataSource source, out byte number)
    {
        if (_fixedSourceNumbers.TryGetValue(source.Value, out number))
            return true;

        lock (_lock)
        {
            if (_assignedSources.TryGetValue(source.Value, out number))
                return true;

            if (TryGetUnknownNumber(source.Value, UnknownPrefix, out number) && !_fixedSources.ContainsKey(number) && !_assignedSourceValues.ContainsKey(number))
                return true;

            number = 0;
            return false;
        }
    }

    /// <summary>
    /// Gets the source a stored number stands for, by its exact value, never
    /// through an alias.
    /// </summary>
    /// <param name="number">The stored number.</param>
    /// <returns>
    /// The source, registered or not, or an unregistered <c>unknown-</c>
    /// source for a number that is neither fixed nor assigned.
    /// </returns>
    public static MetadataSource GetSource(byte number)
        => MetadataSource.GetByValue(TryGetValue(number, out var value) ? value : UnknownPrefix + number.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Whether a stored number is fixed or assigned.
    /// </summary>
    /// <param name="number">The stored number.</param>
    /// <returns><c>true</c> if the number names a source.</returns>
    public static bool IsKnown(byte number)
        => TryGetValue(number, out _);

    /// <summary>
    /// Gets the value a stored number was given, as it was given, without
    /// looking it up among the registered sources.
    /// </summary>
    /// <param name="number">The stored number.</param>
    /// <param name="value">The value, if the number is fixed or assigned.</param>
    /// <returns><c>true</c> if the number names a source.</returns>
    public static bool TryGetValue(byte number, [NotNullWhen(true)] out string? value)
    {
        if (_fixedSources.TryGetValue(number, out value))
            return true;

        lock (_lock)
            return _assignedSourceValues.TryGetValue(number, out value);
    }

    /// <summary>
    /// Tells what is wrong with a number found in a source column, if
    /// anything: it may name no source, or its value may have become an alias
    /// of another source since it was stored.
    /// </summary>
    /// <param name="number">The stored number.</param>
    /// <returns>The problem, or <c>null</c> if the number names its source.</returns>
    public static string? FindProblem(byte number)
    {
        if (!TryGetValue(number, out var value))
            return $"the number {number} names no metadata source, so its rows can no longer be named";

        if (MetadataSource.TryGet(value, out var source) && !string.Equals(source.Value, value, StringComparison.Ordinal))
            return $"the metadata source \"{value}\" stored as {number} is now an alias of \"{source.Value}\", so input naming it reaches \"{source.Value}\" instead";

        return null;
    }

    /// <summary>
    /// Gets the number an entity type is stored as: its fixed number, the one
    /// it was given when it registered, or the one its <c>unknown-</c> value
    /// names if no entity type has that number. Never hands out a number.
    /// </summary>
    /// <param name="entityType">The entity type.</param>
    /// <returns>The stored number.</returns>
    /// <exception cref="InvalidOperationException">
    /// The entity type has no number.
    /// </exception>
    public static byte GetNumber(MetadataEntityType entityType)
    {
        if (_fixedEntityTypeNumbers.TryGetValue(entityType.Value, out var number))
            return number;

        lock (_lock)
        {
            if (_assignedEntityTypes.TryGetValue(entityType.Value, out number))
                return number;

            // Only a number that names no entity type, or the row would move to the entity type that has it.
            if (TryGetUnknownNumber(entityType.Value, UnknownEntityTypePrefix, out number) && !_fixedEntityTypes.ContainsKey(number) && !_assignedEntityTypeValues.ContainsKey(number))
                return number;
        }

        throw new InvalidOperationException(entityType.IsRegistered
            ? $"The metadata entity type \"{entityType.Value}\" has no number, so it can't be stored."
            : $"The metadata entity type \"{entityType.Value}\" is not registered and has no number, so it can't be stored.");
    }

    /// <summary>
    /// Gets the entity type a stored number stands for, by its exact value,
    /// never through an alias.
    /// </summary>
    /// <param name="number">The stored number.</param>
    /// <returns>
    /// The entity type, registered or not, or an unregistered <c>unknown-</c>
    /// entity type for a number that is neither fixed nor assigned.
    /// </returns>
    public static MetadataEntityType GetEntityType(byte number)
        => MetadataEntityType.GetByValue(TryGetEntityTypeValue(number, out var value) ? value : UnknownEntityTypePrefix + number.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Whether a stored entity type number is fixed or assigned.
    /// </summary>
    /// <param name="number">The stored number.</param>
    /// <returns><c>true</c> if the number names an entity type.</returns>
    public static bool IsKnownEntityType(byte number)
        => TryGetEntityTypeValue(number, out _);

    /// <summary>
    /// Gets the value a stored entity type number was given, as it was given,
    /// without looking it up among the registered entity types.
    /// </summary>
    /// <param name="number">The stored number.</param>
    /// <param name="value">The value, if the number is fixed or assigned.</param>
    /// <returns><c>true</c> if the number names an entity type.</returns>
    public static bool TryGetEntityTypeValue(byte number, [NotNullWhen(true)] out string? value)
    {
        if (_fixedEntityTypes.TryGetValue(number, out value))
            return true;

        lock (_lock)
            return _assignedEntityTypeValues.TryGetValue(number, out value);
    }

    /// <summary>
    /// Tells what is wrong with a number found in an entity type column, if
    /// anything: it may name no entity type, or its value may have become an
    /// alias of another entity type since it was stored.
    /// </summary>
    /// <param name="number">The stored number.</param>
    /// <returns>The problem, or <c>null</c> if the number names its entity type.</returns>
    public static string? FindEntityTypeProblem(byte number)
    {
        if (!TryGetEntityTypeValue(number, out var value))
            return $"the number {number} names no metadata entity type, so its rows can no longer be named";

        if (MetadataEntityType.TryGet(value, out var entityType) && !string.Equals(entityType.Value, value, StringComparison.Ordinal))
            return $"the metadata entity type \"{value}\" stored as {number} is now an alias of \"{entityType.Value}\", so input naming it reaches \"{entityType.Value}\" instead";

        return null;
    }

    /// <summary>
    /// Whether the warning about running low on free source numbers was
    /// logged since the last load.
    /// </summary>
    internal static bool HasWarnedAboutFreeNumbers
    {
        get
        {
            lock (_lock)
                return _warnedAboutFreeNumbers;
        }
    }

    /// <summary>
    /// Whether the warning about running low on free entity type numbers was
    /// logged since the last load.
    /// </summary>
    internal static bool HasWarnedAboutFreeEntityTypeNumbers
    {
        get
        {
            lock (_lock)
                return _warnedAboutFreeEntityTypeNumbers;
        }
    }

    // "unknown-N" is how a number without a source or entity type reads back, so it stores as N again.
    private static bool TryGetUnknownNumber(string value, string prefix, out byte number)
    {
        number = 0;
        return value.StartsWith(prefix, StringComparison.Ordinal)
            && byte.TryParse(value.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out number)
            && string.Equals(value, prefix + number.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    // Callers hold the lock.
    private static void WarnIfRunningLow()
    {
        var free = LastFreeSourceNumber - FirstFreeSourceNumber + 1 - _assignedSourceValues.Count;
        if (free < LowFreeNumberCount && !_warnedAboutFreeNumbers)
        {
            _warnedAboutFreeNumbers = true;
            _logger.Warn("Only {Count} numbers are left for new metadata sources; once they run out, no new source can be stored.", free);
        }

        var freeEntityTypes = LastFreeEntityTypeNumber - FirstFreeEntityTypeNumber + 1 - _assignedEntityTypeValues.Count;
        if (freeEntityTypes < LowFreeNumberCount && !_warnedAboutFreeEntityTypeNumbers)
        {
            _warnedAboutFreeEntityTypeNumbers = true;
            _logger.Warn("Only {Count} numbers are left for new metadata entity types; once they run out, no new entity type can be stored.", freeEntityTypes);
        }
    }

    #endregion

    #region File

    /// <summary>
    /// Copies the file next to a database backup, so the two restore as a
    /// pair.
    /// </summary>
    /// <param name="backupFilePath">The database backup's full path.</param>
    public static void CopyWithBackup(string backupFilePath)
    {
        lock (_lock)
        {
            if (_filePath is null || !File.Exists(_filePath))
                return;

            try
            {
                File.Copy(_filePath, backupFilePath + "." + FileName, overwrite: true);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unable to copy {File} next to the database backup {Backup}.", FileName, backupFilePath);
            }
        }
    }

    // Callers hold the lock.
    private static void Save()
    {
        if (_filePath is null || _unreadable)
            return;

        try
        {
            var file = new RegistryFile
            {
                Sources = _assignedSources.OrderBy(pair => pair.Value).ToDictionary(),
                EntityTypes = _assignedEntityTypes.OrderBy(pair => pair.Value).ToDictionary(),
            };
            var tempPath = _filePath + ".tmp";
            File.WriteAllText(tempPath, JsonConvert.SerializeObject(file, Formatting.Indented));
            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Unable to write {File}.", FileName);
        }
    }

    private sealed class RegistryFile
    {
        public Dictionary<string, byte> Sources { get; set; } = [];

        public Dictionary<string, byte> EntityTypes { get; set; } = [];
    }

    #endregion
}
