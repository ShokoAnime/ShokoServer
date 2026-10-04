using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;
using Namotion.Reflection;
using Shoko.Abstractions.Extensions;

namespace Shoko.Server.Plugin;

public static partial class TypeReflectionExtensions
{
    /// <summary>
    /// Held around every read of the XML docs through Namotion.Reflection. It
    /// caches each assembly's docs once per process and rewrites the cached
    /// elements in place while it resolves <c>inheritdoc</c>, so two readers at
    /// once can corrupt them and leave a later read looping forever.
    /// </summary>
    internal static readonly object XmlDocsLock = new();

    /// <summary>
    /// Drops the XML docs Namotion.Reflection keeps parsed for every assembly
    /// it has read, about 28 MB with the bundled plugins. A later read parses
    /// the file again.
    /// </summary>
    internal static void ReleaseXmlDocs()
    {
        lock (XmlDocsLock)
            XmlDocs.ClearCache();
    }

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
    /// <param name="transform">The transform function.</param>
    public static string GetDisplayName(this ContextualMemberInfo memberInfo, Func<string, string>? transform = null)
    {
        var displayAttribute = memberInfo.GetAttribute<DisplayAttribute>(false);
        var displayName = displayAttribute != null && !string.IsNullOrEmpty(displayAttribute.Name)
            ? displayAttribute.Name
            : DisplayNameRegex().Replace(memberInfo.Name, " $1").Transform(transform);

        return displayName;
    }

    /// <summary>
    /// Gets the display name for a type.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="transform">The transform function.</param>
    public static string GetDisplayName(this ContextualType type, Func<string, string>? transform = null)
    {
        var displayAttribute = type.GetAttribute<DisplayAttribute>(false);
        var displayName = displayAttribute != null && !string.IsNullOrEmpty(displayAttribute.Name)
            ? displayAttribute.Name
            : DisplayNameRegex().Replace(type.Name, " $1").Transform(transform);

        return displayName;
    }

    /// <summary>
    /// Gets the display name for a string.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="transform">The transform function.</param>
    public static string GetDisplayName(this string name, Func<string, string>? transform = null)
        => DisplayNameRegex().Replace(name, " $1")
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(s => char.ToUpperInvariant(s[0]) + s[1..])
            .Join(' ')
            .Transform(transform);

    private static string Transform(this string displayName, Func<string, string>? transform)
        => transform is null ? displayName : transform(displayName);

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
        {
            lock (XmlDocsLock)
                description = memberInfo.GetXmlDocsSummary() ?? string.Empty;
        }

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
        {
            lock (XmlDocsLock)
                description = type.GetXmlDocsSummary() ?? string.Empty;
        }

        return CleanDescription(description);
    }

    public static string CleanDescription(this string description)
        => description
            .WriteKeywordsAsCode()
            .Replace(BreakTwoRegex(), "\0")
            .Replace(BreakRegex(), " ")
            .Replace(SpaceRegex(), " ")
            .Replace("\0", "\n")
            .Replace("\n ", "\n")
            .Trim();

    /// <summary>
    /// Writes the language keywords in a description as inline code, in place
    /// of the links to the keyword's documentation that Namotion.Reflection
    /// writes for a <c>see langword</c> tag in Markdown.
    /// </summary>
    /// <param name="description">The description.</param>
    /// <returns>The description with each keyword link as inline code.</returns>
    public static string WriteKeywordsAsCode(this string description)
        => description.Replace(KeywordLinkRegex(), "`$1`");

    /// <summary>
    /// Matches a Markdown link to the C# documentation of a language keyword.
    /// </summary>
    [GeneratedRegex(@"\[([^\]]+)\]\(https?://learn\.microsoft\.com/[^)\s]*/language-reference/keywords/\1/?\)")]
    private static partial Regex KeywordLinkRegex();

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
