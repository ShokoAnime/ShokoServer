using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Shoko.QueueProcessor.Builder;

/// <summary>
/// Names a job type the same way everywhere the queue stores or shows it.
/// </summary>
/// <remarks>
/// A plain type keeps the names it always had. A closed generic job type, such as one job type
/// per provider, is named after its definition and its type arguments, without assembly
/// versions, so a stored job still finds its type after a plugin is updated.
/// </remarks>
public static partial class JobTypeNames
{
    #region Names

    /// <summary>
    /// The name a job type is stored under: its full name, then a comma and the name of the
    /// assembly that defines it.
    /// </summary>
    /// <param name="type">The job type.</param>
    /// <returns>The stored name, e.g. <c>Namespace.Job`1[[Namespace.Provider, Plugin]], Server</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="type"/> is <c>null</c>.</exception>
    public static string Stored(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return Full(type) + ", " + type.Assembly.GetName().Name;
    }

    /// <summary>
    /// The full name of a job type, with each type argument of a closed generic type written as
    /// its own stored name.
    /// </summary>
    /// <param name="type">The job type.</param>
    /// <returns>The full name, without any assembly version.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="type"/> is <c>null</c>.</exception>
    public static string Full(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (!type.IsConstructedGenericType)
            return type.FullName ?? type.Name;

        var definition = type.GetGenericTypeDefinition();
        var arguments = string.Join(",", type.GenericTypeArguments.Select(argument => "[" + Stored(argument) + "]"));
        return $"{definition.FullName}[{arguments}]";
    }

    /// <summary>
    /// The short name of a job type, for logs and display only.
    /// </summary>
    /// <remarks>
    /// Two closed types whose type arguments share a simple name get the same short name, so
    /// anything that must tell job types apart uses <see cref="Key"/> instead.
    /// </remarks>
    /// <param name="type">The job type.</param>
    /// <returns>
    /// The type's name, or for a closed generic type its name with the type arguments' short
    /// names, e.g. <c>RefreshMetadataJob&lt;MyProvider&gt;</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="type"/> is <c>null</c>.</exception>
    public static string Short(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (!type.IsConstructedGenericType)
            return type.Name;

        var name = type.Name;
        var tick = name.IndexOf('`');
        if (tick >= 0)
            name = name[..tick];

        return $"{name}<{string.Join(",", type.GenericTypeArguments.Select(Short))}>";
    }

    /// <summary>
    /// The name that tells job types apart, which keys the pools, the concurrency overrides and
    /// the metrics.
    /// </summary>
    /// <param name="type">The job type.</param>
    /// <returns>
    /// The type's name, or for a closed generic type its name with each type argument's full
    /// name, e.g. <c>RefreshMetadataJob&lt;My.Plugin.MyProvider&gt;</c>, so two plugins' providers
    /// of the same simple name never share a pool.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="type"/> is <c>null</c>.</exception>
    public static string Key(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (!type.IsConstructedGenericType)
            return type.Name;

        var name = type.Name;
        var tick = name.IndexOf('`');
        if (tick >= 0)
            name = name[..tick];

        return $"{name}<{string.Join(",", type.GenericTypeArguments.Select(Full))}>";
    }

    #endregion

    #region Assemblies

    /// <summary>
    /// Every assembly a stored name mentions: the one defining the type, and for a closed generic
    /// type the ones defining its type arguments.
    /// </summary>
    /// <param name="storedName">A name as <see cref="Stored"/> writes it.</param>
    /// <returns>The assembly names, the defining assembly first.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="storedName"/> is <c>null</c>.</exception>
    public static IReadOnlyList<string> AssemblyNames(string storedName)
    {
        ArgumentNullException.ThrowIfNull(storedName);
        var names = new List<string>();
        var index = storedName.LastIndexOf(", ", StringComparison.Ordinal);
        if (index < 0 || storedName.IndexOf(']', index) >= 0)
            return names;

        names.Add(storedName[(index + 2)..]);
        foreach (Match match in ArgumentPattern().Matches(storedName[..index]))
            names.Add(match.Groups["assembly"].Value);

        return names;
    }

    [GeneratedRegex(@"\[(?<type>[^\[\],]+), (?<assembly>[^\[\],]+)\]")]
    private static partial Regex ArgumentPattern();

    #endregion
}
