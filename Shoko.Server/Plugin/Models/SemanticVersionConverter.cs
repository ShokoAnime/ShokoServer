using System;
using System.Text.Json;
using Newtonsoft.Json;

namespace Shoko.Server.Plugin.Models;

/// <summary>
///   Parses version strings in formats like <c>1</c>, <c>1.0</c>,
///   <c>1.0.0</c>, <c>1.0.0.0</c>, or <c>1.0.0-dev.0</c> into a
///   <see cref="Version"/>.
///
///   The <c>-dev.N</c> suffix is mapped to the <see cref="Version.Revision"/>
///   component. A version with -dev.0 has revision 0; missing components
///   default to 0. Stable versions with all components present serialize
///   as-is; dev versions append <c>-dev.N</c>.
/// </summary>
public class SemanticVersionConverter : System.Text.Json.Serialization.JsonConverter<Version>
{
    public override Version Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new System.Text.Json.JsonException("Expected a string value for version.");

        var raw = reader.GetString();
        if (string.IsNullOrWhiteSpace(raw))
            throw new System.Text.Json.JsonException("Version string cannot be empty.");

        if (TryParseVersion(raw, out var result))
            return result;

        throw new System.Text.Json.JsonException($"Invalid version format: '{raw}'.");
    }

    public override void Write(Utf8JsonWriter writer, Version value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(FormatVersion(value));
    }

    /// <summary>
    ///   Parse a version string in the extended format. Returns <c>true</c>
    ///   on success.
    /// </summary>
    public static bool TryParseVersion(string input, out Version result)
    {
        result = new Version(0, 0, 0);

        if (string.IsNullOrWhiteSpace(input))
            return false;

        // Strip -dev.N suffix if present, capture revision
        var devRevision = 0;
        var body = input;
        var devIdx = input.IndexOf("-dev.", StringComparison.Ordinal);

        if (devIdx >= 0)
        {
            var suffix = input[(devIdx + 5)..];
            if (!int.TryParse(suffix, out devRevision) || devRevision < 0)
                return false;

            body = input[..devIdx];
        }

        // Parse numeric parts: "1", "1.0", "1.0.0", "1.0.0.0"
        var parts = body.Split('.');
        if (parts.Length > 4)
            return false;

        var numbers = new int[4];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out numbers[i]) || numbers[i] < 0)
                return false;
        }

        // -dev.N sets the revision
        numbers[3] = numbers[3] > 0 ? numbers[3] : devRevision;

        result = numbers[3] > 0
            ? new Version(numbers[0], numbers[1], numbers[2], numbers[3])
            : new Version(numbers[0], numbers[1], numbers[2]);
        return true;
    }

    /// <summary>
    ///   Convert a <see cref="Version"/> to manifest string format.
    ///   Stable releases omit suffix; dev releases append <c>-dev.N</c>.
    /// </summary>
    public static string FormatVersion(Version version)
    {
        var major = version.Major;
        var minor = version.Minor;
        var build = version.Build;
        var revision = version.Revision;

        if (revision > 0)
            return $"{major}.{minor}.{build}-dev.{revision}";

        return $"{major}.{minor}.{build}";
    }
}

/// <summary>
///   Newtonsoft.Json converter that delegates to
///   <see cref="SemanticVersionConverter"/>.
/// </summary>
public class SemanticVersionNewtonsoftConverter : JsonConverter<Version>
{
    public override Version? ReadJson(JsonReader reader, Type objectType, Version? existingValue, bool hasExistingValue, Newtonsoft.Json.JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;

        if (reader.TokenType == JsonToken.String && reader.Value is string raw && !string.IsNullOrWhiteSpace(raw))
        {
            if (SemanticVersionConverter.TryParseVersion(raw, out var result))
                return result;
        }

        throw new Newtonsoft.Json.JsonException("Invalid version format.");
    }

    public override void WriteJson(JsonWriter writer, Version? value, Newtonsoft.Json.JsonSerializer serializer)
    {
        if (value is null)
            writer.WriteNull();
        else
            writer.WriteValue(SemanticVersionConverter.FormatVersion(value));
    }
}
