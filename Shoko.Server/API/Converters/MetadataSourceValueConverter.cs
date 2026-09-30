using System;
using Newtonsoft.Json;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Converters;

namespace Shoko.Server.API.Converters;

/// <summary>
///   Writes a <see cref="MetadataSource"/> for the API as the old enum spelled
///   it, through <see cref="LegacyMetadataSpellings"/>, and reads the value or
///   an alias of a registered source only, ignoring case, or of any source
///   for settings.
/// </summary>
public sealed class MetadataSourceValueConverter : MetadataSourceNewtonsoftJsonConverter
{
    private readonly bool _registeredOnly;

    private MetadataSourceValueConverter(bool registeredOnly)
    {
        _registeredOnly = registeredOnly;
    }

    /// <summary>
    ///   The shared instance, which reads registered sources only.
    /// </summary>
    public static MetadataSourceValueConverter Instance { get; } = new(registeredOnly: true);

    /// <summary>
    ///   The shared instance for settings, which also reads a source whose
    ///   plugin isn't loaded, as the settings file itself does.
    /// </summary>
    public static MetadataSourceValueConverter Lenient { get; } = new(registeredOnly: false);

    /// <summary>
    ///   Reads a source, refusing text that names no registered source unless
    ///   this is the <see cref="Lenient"/> converter.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="objectType">The type to read.</param>
    /// <param name="existingValue">Ignored.</param>
    /// <param name="hasExistingValue">Ignored.</param>
    /// <param name="serializer">Ignored.</param>
    /// <returns>The source, or <c>null</c> for a JSON null.</returns>
    /// <exception cref="JsonSerializationException">
    ///   The token is not a string, or it names no source this converter takes.
    /// </exception>
    public override MetadataSource? ReadJson(JsonReader reader, Type objectType, MetadataSource? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (!_registeredOnly)
            return base.ReadJson(reader, objectType, existingValue, hasExistingValue, serializer);
        if (reader.TokenType is JsonToken.Null)
            return null;
        if (reader.TokenType is not JsonToken.String)
            throw new JsonSerializationException($"Expected a string for a metadata source, got {reader.TokenType}.");

        return ReadRegistered((string?)reader.Value);
    }

    /// <summary>
    ///   Gets the text written for a source: its old spelling, or its value
    ///   when the old enum lacked it.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The text.</returns>
    protected override string GetText(MetadataSource source)
        => LegacyMetadataSpellings.Of(source);

    /// <summary>
    ///   Gets a registered source by its value or an alias, ignoring case.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The registered source.</returns>
    /// <exception cref="JsonSerializationException">
    ///   No source is registered under <paramref name="text"/>.
    /// </exception>
    internal static MetadataSource ReadRegistered(string? text)
        => MetadataSource.TryGet(text, out var source)
            ? source
            : throw new JsonSerializationException($"\"{text}\" is not a registered metadata source.");
}
