using System;
using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions;

/// <summary>
/// Plugin utilities.
/// </summary>
public static class PluginUtilities
{
    /// <summary>
    /// The mapping used for replacing invalid path characters with unicode alternatives
    /// </summary>
    public static Dictionary<string, string> InvalidPathCharacterMap = new()
    {
        { "*", "\u2605" }, // ★ (BLACK STAR)
        { "|", "\u00a6" }, // ¦ (BROKEN BAR)
        { "\\", "\u29F9" }, // ⧹ (BIG REVERSE SOLIDUS)
        { "/", "\u2044" }, // ⁄ (FRACTION SLASH)
        { ":", "\u0589" }, // ։ (ARMENIAN FULL STOP)
        { "\"", "\u2033" }, // ″ (DOUBLE PRIME)
        { ">", "\u203a" }, // › (SINGLE RIGHT-POINTING ANGLE QUOTATION MARK)
        { "<", "\u2039" }, // ‹ (SINGLE LEFT-POINTING ANGLE QUOTATION MARK)
        { "?", "\uff1f" }, // ？ (FULL WIDTH QUESTION MARK)
    };
    
    /// <summary>
    /// Remove invalid path characters.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The sanitized path.</returns>
    public static string RemoveInvalidPathCharacters(this string path)
    {
        var ret = path;
        foreach (var key in InvalidPathCharacterMap.Keys)
            ret = ret.Replace(key, string.Empty);
        while (ret.EndsWith(".", StringComparison.Ordinal))
            ret = ret.Substring(0, ret.Length - 1);
        return ret.Trim();
    }

    /// <summary>
    /// Replace invalid path characters.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The sanitized path.</returns>
    public static string ReplaceInvalidPathCharacters(this string path)
    {
        var ret = path;
        foreach (var kvp in InvalidPathCharacterMap)
            ret = ret.Replace(kvp.Key, kvp.Value);
        ret = ret.Replace("...", "\u2026"); // … (HORIZONTAL ELLIPSIS))
        if (ret.StartsWith(".", StringComparison.Ordinal)) // U+002E
            ret = "․" + ret.Substring(1, ret.Length - 1); // U+2024
        if (ret.EndsWith(".", StringComparison.Ordinal)) // U+002E
            ret = ret.Substring(0, ret.Length - 1) + "․"; // U+2024
        return ret.Trim();
    }

    /// <summary>
    /// Pads the number with zeroes and returns it as a string.
    /// </summary>
    /// <param name="num">Number to pad.</param>
    /// <param name="total">The highest number that num can be, used to determine how many zeroes to add.</param>
    /// <returns>The padded number as a string.</returns>
    public static string PadZeroes(this int num, int total)
    {
        var zeroPadding = total.ToString().Length;
        return num.ToString().PadLeft(zeroPadding, '0');
    }
}
