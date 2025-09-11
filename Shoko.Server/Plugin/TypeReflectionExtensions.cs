
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;
using Namotion.Reflection;
using Shoko.Server.Extensions;

#nullable enable
namespace Shoko.Server.Plugin;

public static partial class TypeReflectionExtensions
{
    /// <summary>
    /// Gets the display name for a type.
    /// </summary>
    /// <param name="type">The type.</param>
    public static string GetDisplayName(this Type type)
        => GetDisplayName(type.ToContextualType());

    /// <summary>
    /// Gets the display name for a member.
    /// </summary>
    /// <param name="memberInfo">The member info.</param>
    public static string GetDisplayName(this ContextualMemberInfo memberInfo)
    {
        var displayAttribute = memberInfo.GetAttribute<DisplayAttribute>(false);
        var displayName = displayAttribute != null && !string.IsNullOrEmpty(displayAttribute.Name)
            ? displayAttribute.Name
            : DisplayNameRegex().Replace(memberInfo.Name, " $1");

        return displayName;
    }
    /// <summary>
    /// Gets the display name for a type.
    /// </summary>
    /// <param name="type">The type.</param>
    public static string GetDisplayName(this ContextualType type)
    {
        var displayAttribute = type.GetAttribute<DisplayAttribute>(false);
        var displayName = displayAttribute != null && !string.IsNullOrEmpty(displayAttribute.Name)
            ? displayAttribute.Name
            : DisplayNameRegex().Replace(type.Name, " $1");

        return displayName;
    }

    public static string GetDisplayName(this string name)
        => DisplayNameRegex().Replace(name, " $1")
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(s => char.ToUpperInvariant(s[0]) + s[1..])
            .Join(' ');

    /// <summary>
    /// Simple regex to auto-infer display name from PascalCase class names.
    /// </summary>
    [GeneratedRegex(@"(\B[A-Z](?![A-Z]))\B")]
    private static partial Regex DisplayNameRegex();

    /// <summary>
    /// Gets the description for a type.
    /// </summary>
    /// <param name="type">The type.</param>
    public static string GetDescription(this Type type)
        => GetDescription(type.ToContextualType());

    /// <summary>
    /// Gets the description for a member.
    /// </summary>
    /// <param name="memberInfo">The member info.</param>
    public static string GetDescription(this ContextualMemberInfo memberInfo)
    {
        var description = memberInfo.GetAttribute<DisplayAttribute>(false)?.Description;
        if (string.IsNullOrEmpty(description))
            description = memberInfo.GetXmlDocsSummary() ?? string.Empty;

        return CleanDescription(description);
    }

    /// <summary>
    /// Gets the description for a type.
    /// </summary>
    /// <param name="type">The type.</param>
    public static string GetDescription(this ContextualType type)
    {
        var description = type.GetAttribute<DisplayAttribute>(false)?.Description;
        if (string.IsNullOrEmpty(description))
            description = type.GetXmlDocsSummary() ?? string.Empty;

        return CleanDescription(description);
    }

    public static string CleanDescription(this string description)
        => description
            .Replace(BreakTwoRegex(), "\0")
            .Replace(BreakRegex(), " ")
            .Replace(SpaceRegex(), " ")
            .Replace("\0", "\n")
            .Replace("\n ", "\n")
            .Trim();

    /// <summary>
    /// Simple regex to collapse multiple lines into a single line.
    /// </summary>
    [GeneratedRegex(@"(\r\n|\r|\n){2,}")]
    private static partial Regex BreakTwoRegex();

    /// <summary>
    /// Simple regex to convert single line breaks to spaces.
    /// </summary>
    [GeneratedRegex(@"(\r\n|\r|\n)")]
    private static partial Regex BreakRegex();

    /// <summary>
    /// Simple regex to convert multiple spaces or tabs to a single space.
    /// </summary>
    [GeneratedRegex(@"[\t ]+")]
    private static partial Regex SpaceRegex();
}
