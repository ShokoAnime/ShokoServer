using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Scheduling.Jobs.Metadata;

/// <summary>
///   Finds the registered provider a provider job runs for, and describes
///   the job's entry for the queue.
/// </summary>
internal static class MetadataProviderJobContext
{
    /// <summary>
    ///   The registered info of a provider type, or <c>null</c>
    ///   when no provider of the type is registered.
    /// </summary>
    /// <typeparam name="TProvider">The provider's type.</typeparam>
    /// <param name="providerManager">Where the providers are registered.</param>
    /// <returns>The info, or <c>null</c>.</returns>
    public static MetadataProviderInfo? Find<TProvider>(IMetadataProviderManager providerManager)
        where TProvider : class, IMetadataProvider
    {
        try
        {
            return providerManager.GetProviderInfo(typeof(TProvider));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    ///   Adds an entry to a job's details as its source, kind and ID, each in
    ///   its display form, or as the text given when it is no entry ID.
    /// </summary>
    /// <param name="details">The details to add to.</param>
    /// <param name="entryID">The entry, as its <see cref="MetadataGuid"/> string.</param>
    /// <returns>The same details.</returns>
    public static Dictionary<string, object> WithEntry(this Dictionary<string, object> details, string? entryID)
    {
        if (string.IsNullOrEmpty(entryID))
            return details;

        if (MetadataGuid.TryParse(entryID, out var entry))
        {
            details["Source"] = entry.Source.Name;
            details["Kind"] = entry.EntityType.Name;
            details["ID"] = entry.ID;
        }
        else
        {
            details["Entry"] = entryID;
        }

        return details;
    }

    /// <summary>
    ///   The registered provider of a type, with its info.
    /// </summary>
    /// <typeparam name="TProvider">The provider's type.</typeparam>
    /// <param name="providerManager">Where the providers are registered.</param>
    /// <param name="logger">Where a missing provider is reported.</param>
    /// <returns>
    ///   The info and the provider, or <c>null</c> when no provider
    ///   of the type was registered, such as when its plugin refused a
    ///   reserved source.
    /// </returns>
    public static (MetadataProviderInfo Info, TProvider Provider)? Resolve<TProvider>(IMetadataProviderManager providerManager, ILogger logger)
        where TProvider : class, IMetadataProvider
    {
        try
        {
            var info = providerManager.GetProviderInfo(typeof(TProvider));
            if (info.Provider is TProvider provider)
                return (info, provider);
        }
        catch (ArgumentException)
        {
        }

        logger.LogWarning("Skipping a job for {Provider}, which is not a registered metadata provider.", typeof(TProvider).Name);
        return null;
    }
}
