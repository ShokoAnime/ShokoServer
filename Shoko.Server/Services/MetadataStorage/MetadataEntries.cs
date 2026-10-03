using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;

#pragma warning disable CS0618
namespace Shoko.Server.Services.MetadataStorage;

/// <summary>
///   Finds the entries the typed metadata stores point at, and tells which
///   kind of entry a requested model type stands for.
/// </summary>
internal static class MetadataEntries
{
    #region Resolving

    /// <summary>
    ///   The entry a stored row points at, as the core resolves it.
    /// </summary>
    /// <param name="source">The source the entry belongs to.</param>
    /// <param name="entityType">What kind of entry it is.</param>
    /// <param name="entityID">The source's own ID for it.</param>
    /// <returns>The entry, or <c>null</c> when the ID is not valid or nothing holds it.</returns>
    internal static IMetadata? Resolve(MetadataSource source, MetadataEntityType entityType, string? entityID)
        => ToGuid(source, entityType, entityID) is { } id ? Resolve(id) : null;

    /// <summary>
    ///   The entry an identifier names, as the core resolves it.
    /// </summary>
    /// <param name="id">The entry's identifier.</param>
    /// <returns>The entry, or <c>null</c> when nothing holds it.</returns>
    internal static IMetadata? Resolve(MetadataGuid id)
        => ISystemService.StaticServices.GetService<IMetadataService>()?.GetEntry(id);

    /// <summary>
    ///   The identifier stored columns name, when the ID they hold is valid.
    /// </summary>
    /// <param name="source">The source the entry belongs to.</param>
    /// <param name="entityType">What kind of entry it is.</param>
    /// <param name="entityID">The source's own ID for it, as stored.</param>
    /// <returns>The identifier, or <c>null</c> when the ID is empty or not valid.</returns>
    internal static MetadataGuid? ToGuid(MetadataSource source, MetadataEntityType entityType, string? entityID)
        => string.IsNullOrEmpty(entityID) ||
            entityID.Length > MetadataGuid.MaxIDLength ||
            char.IsWhiteSpace(entityID[0]) ||
            char.IsWhiteSpace(entityID[^1])
                ? null
                : new(source, entityType, entityID);

    #endregion

    #region Typing

    /// <summary>
    ///   The kind of entry a model type stands for, so a typed read can skip
    ///   the rows that could never be of that type.
    /// </summary>
    /// <typeparam name="T">The model type asked for.</typeparam>
    /// <returns>
    ///   The kind of entry, or <c>null</c> when the type matches every kind,
    ///   as <see cref="IMetadata"/> does.
    /// </returns>
    internal static MetadataEntityType? TypeOf<T>() where T : IMetadata
        => TypeOf(typeof(T));

    /// <summary>
    ///   The kind of entry a model type stands for.
    /// </summary>
    /// <param name="type">The model type asked for.</param>
    /// <returns>
    ///   The kind of entry, or <c>null</c> when the type does not narrow it
    ///   to one kind.
    /// </returns>
    internal static MetadataEntityType? TypeOf(Type type)
    {
        if (type.IsAssignableTo(typeof(ISeries)))
            return MetadataEntityType.Series;
        if (type.IsAssignableTo(typeof(IMovie)))
            return MetadataEntityType.Movie;
        if (type.IsAssignableTo(typeof(IEpisode)))
            return MetadataEntityType.Episode;
        if (type.IsAssignableTo(typeof(ISeason)))
            return MetadataEntityType.Season;
        return null;
    }

    /// <summary>
    ///   Whether a row's kind of entry fits the kind asked for.
    /// </summary>
    /// <param name="wanted">The kind asked for, or <c>null</c> for any.</param>
    /// <param name="entityType">The row's kind of entry.</param>
    /// <returns>Whether the row fits.</returns>
    internal static bool Fits(MetadataEntityType? wanted, MetadataEntityType entityType)
        => wanted is not { } only || only == entityType;

    #endregion

    #region Validation

    /// <summary>
    ///   The longest language code the stores' tables hold.
    /// </summary>
    internal const int MaxLanguageCodeLength = 32;

    /// <summary>
    ///   Refuses a write under a source the core registers and keeps in its
    ///   own tables: <c>shoko</c>, <c>user</c>, <c>generated</c> and
    ///   <c>anidb</c>. Every typed store checks the source it
    ///   writes for with this before anything is written.
    /// </summary>
    /// <param name="source">The source written for.</param>
    /// <param name="paramName">The argument it came in through.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The source is one the core keeps itself.</exception>
    internal static void CheckWritableSource(MetadataSource source, string paramName)
    {
        ArgumentNullException.ThrowIfNull(source, paramName);
        if (source.IsCore)
            throw new ArgumentException($"The source \"{source.Value}\" is kept by the core, so the metadata stores do not write under it.", paramName);
    }

    /// <summary>
    ///   Checks an entry that a write names, such as a credit's creator or a
    ///   related entry, before anything is written.
    /// </summary>
    /// <param name="reference">The entry named.</param>
    /// <param name="source">The source it must be on: the one being written for.</param>
    /// <param name="entityType">The kind it must be, or <c>null</c> for any kind.</param>
    /// <param name="paramName">The argument it came in through.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The entry is on another source, or of another kind.</exception>
    internal static void CheckReference(MetadataGuid? reference, MetadataSource source, MetadataEntityType? entityType, string paramName)
    {
        ArgumentNullException.ThrowIfNull(reference, paramName);
        if (reference.Source != source)
            throw new ArgumentException($"\"{reference}\" is not on {source.Value}, the source being written for.", paramName);
        if (entityType is not null && reference.EntityType != entityType)
            throw new ArgumentException($"\"{reference}\" does not name a {entityType.Value}.", paramName);
    }

    /// <summary>
    ///   Checks the entry a write is for: that it is given, is of the kind
    ///   the store keeps, and is on a source the stores may write under.
    /// </summary>
    /// <param name="id">The entry.</param>
    /// <param name="entityType">The kind it must be.</param>
    /// <param name="paramName">The argument it came in through.</param>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    ///   The entry is of another kind, or on a source the core keeps itself.
    /// </exception>
    internal static void CheckEntry(MetadataGuid? id, MetadataEntityType entityType, string paramName)
    {
        ArgumentNullException.ThrowIfNull(id, paramName);
        if (id.EntityType != entityType)
            throw new ArgumentException($"\"{id}\" does not name a {entityType.Value}.", paramName);
        CheckWritableSource(id.Source, paramName);
    }

    /// <summary>
    ///   Checks a language code before it is written.
    /// </summary>
    /// <param name="languageCode">The language code, which may be left out.</param>
    /// <param name="paramName">The argument it came in through.</param>
    /// <returns>The code, or <c>null</c> when it was left out or blank.</returns>
    /// <exception cref="ArgumentException">The code is too long.</exception>
    internal static string? CheckLanguageCode(string? languageCode, string paramName)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            return null;
        if (languageCode.Length > MaxLanguageCodeLength)
            throw new ArgumentException($"A language code must be at most {MaxLanguageCodeLength} characters, but got '{languageCode}'.", paramName);
        return languageCode;
    }

    /// <summary>
    ///   Checks the production countries given for an entry before they are
    ///   written, dropping blank and repeated ones.
    /// </summary>
    /// <param name="countries">The countries, which may be left out.</param>
    /// <param name="paramName">The argument they came in through.</param>
    /// <returns>The countries to store, trimmed, in order.</returns>
    /// <exception cref="ArgumentNullException">A country is <c>null</c>.</exception>
    internal static List<string> CheckCountries(IReadOnlyList<string>? countries, string paramName)
    {
        if (countries is null)
            return [];

        foreach (var country in countries)
            ArgumentNullException.ThrowIfNull(country, paramName);
        return [.. countries.Select(country => country.Trim()).Where(country => country.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    ///   Checks the links given for an entry before they are written.
    /// </summary>
    /// <param name="resources">The links, which may be left out.</param>
    /// <param name="paramName">The argument they came in through.</param>
    /// <returns>A copy of the links to store.</returns>
    /// <exception cref="ArgumentNullException">A link is <c>null</c>.</exception>
    internal static List<Resource> CheckResources(IReadOnlyList<Resource>? resources, string paramName)
    {
        if (resources is null)
            return [];

        foreach (var resource in resources)
            ArgumentNullException.ThrowIfNull(resource, paramName);
        return [.. resources];
    }

    /// <summary>
    ///   Checks the IDs other sources gave an entry before they are written.
    /// </summary>
    /// <param name="ids">The IDs, which may be left out.</param>
    /// <param name="paramName">The argument they came in through.</param>
    /// <returns>The IDs to store, each once, in the order given.</returns>
    /// <exception cref="ArgumentNullException">An ID is <c>null</c>.</exception>
    internal static List<MetadataGuid> CheckCrossSourceIDs(IReadOnlyList<MetadataGuid>? ids, string paramName)
    {
        if (ids is null)
            return [];

        foreach (var id in ids)
            ArgumentNullException.ThrowIfNull(id, paramName);
        return [.. ids.Distinct()];
    }

    #endregion
}
