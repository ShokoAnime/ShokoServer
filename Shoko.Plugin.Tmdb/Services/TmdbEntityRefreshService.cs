using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Plugin.Tmdb.Api;
using Shoko.Plugin.Tmdb.Mapping;

using TmdbImageData = TMDbLib.Objects.General.ImageData;

namespace Shoko.Plugin.Tmdb.Services;

/// <summary>
///   Refreshes TMDb's people, companies and networks one at a time, when the
///   core asks for a stub or a stale entry.
/// </summary>
/// <remarks>
///   A person is fetched with their translations, external IDs and photos;
///   the photos are kept a while for the image job that follows. Companies
///   and networks are written in full by the refresh that names them, so
///   this only brings them up to date once they go stale.
/// </remarks>
/// <param name="apiClient">The TMDb client.</param>
/// <param name="stores">The core's stores.</param>
/// <param name="configurationProvider">The plugin's configuration.</param>
/// <param name="logger">The logger.</param>
public sealed class TmdbEntityRefreshService(
    TmdbApiClient apiClient,
    TmdbStores stores,
    ConfigurationProvider<TmdbConfiguration> configurationProvider,
    ILogger<TmdbEntityRefreshService> logger
)
{
    #region Fields

    /// <summary>
    ///   How long the photos fetched with a person, and the logo of a
    ///   company, are kept for the image job.
    /// </summary>
    private static readonly TimeSpan _imagesFreshFor = TimeSpan.FromHours(2);

    private readonly ConcurrentDictionary<int, (DateTimeOffset FetchedAt, IReadOnlyList<TmdbImageData> Images)> _personImages = new();

    private readonly ConcurrentDictionary<int, (DateTimeOffset FetchedAt, string? LogoPath)> _companyLogos = new();

    #endregion

    #region Refresh

    /// <summary>
    ///   Fetches a person, company or network and writes it into the stores.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>Whether TMDb had it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entityID"/> is <c>null</c>.</exception>
    /// <exception cref="MetadataProviderNotConfiguredException">No API key is configured.</exception>
    public Task<bool> Refresh(MetadataGuid entityID, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entityID);

        if (TmdbIds.TryGetID(entityID, MetadataEntityType.Creator, out var personID))
            return RefreshPerson(personID, cancellationToken);
        if (TmdbIds.TryGetID(entityID, MetadataEntityType.Studio, out var companyID))
            return RefreshCompany(companyID, cancellationToken);
        if (TmdbIds.TryGetID(entityID, MetadataEntityType.Network, out var networkID))
            return RefreshNetwork(networkID, cancellationToken);

        return Task.FromResult(false);
    }

    /// <summary>
    ///   Fetches a person and writes them, with their biographies in the
    ///   languages kept, the English one on the person itself.
    /// </summary>
    /// <param name="personID">The TMDb person ID.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>Whether TMDb had the person.</returns>
    public async Task<bool> RefreshPerson(int personID, CancellationToken cancellationToken = default)
    {
        if (await apiClient.GetPerson(personID, cancellationToken).ConfigureAwait(false) is not { } person)
        {
            logger.LogDebug("TMDb has no person with ID {PersonID}.", personID);
            return false;
        }

        var creator = TmdbEntityMapper.ToCreatorData(person) with { ID = TmdbIds.Creator(personID) };
        stores.People.SaveCreators([creator]);

        // The English biography is the person's own; the others go through the text manager.
        var languages = TmdbTextLanguages.From(configurationProvider.Load(), stores.Texts);
        var overviews = TmdbTexts.Overviews(creator.Overview, person.Translations, languages.Overviews)
            .Where(overview => !(overview.LanguageCode is "en" && overview.CountryCode is "US"))
            .ToList();
        stores.Texts.SetOverviews(creator.ID, MetadataSource.TMDB, overviews);

        if (person.Images?.Profiles is { } profiles)
            _personImages[personID] = (apiClient.TimeProvider.GetUtcNow(), profiles);

        logger.LogDebug("Refreshed TMDb person {PersonID} ({Name}).", personID, person.Name);
        return true;
    }

    /// <summary>
    ///   Fetches a company and writes it as a studio.
    /// </summary>
    /// <param name="companyID">The TMDb company ID.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>Whether TMDb had the company.</returns>
    public async Task<bool> RefreshCompany(int companyID, CancellationToken cancellationToken = default)
    {
        if (await apiClient.GetCompany(companyID, cancellationToken).ConfigureAwait(false) is not { } company)
        {
            logger.LogDebug("TMDb has no company with ID {CompanyID}.", companyID);
            return false;
        }

        stores.Studios.SaveStudios([TmdbEntityMapper.ToStudioData(companyID, company.Name, company.OriginCountry, company.LogoPath)]);
        _companyLogos[companyID] = (apiClient.TimeProvider.GetUtcNow(), company.LogoPath);
        logger.LogDebug("Refreshed TMDb company {CompanyID} ({Name}).", companyID, company.Name);
        return true;
    }

    /// <summary>
    ///   Fetches a network and writes it.
    /// </summary>
    /// <param name="networkID">The TMDb network ID.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>Whether TMDb had the network.</returns>
    public async Task<bool> RefreshNetwork(int networkID, CancellationToken cancellationToken = default)
    {
        if (await apiClient.GetNetwork(networkID, cancellationToken).ConfigureAwait(false) is not { } network)
        {
            logger.LogDebug("TMDb has no network with ID {NetworkID}.", networkID);
            return false;
        }

        stores.Studios.SaveNetworks([TmdbEntityMapper.ToNetworkData(networkID, network.Name, network.OriginCountry)]);
        logger.LogDebug("Refreshed TMDb network {NetworkID} ({Name}).", networkID, network.Name);
        return true;
    }

    #endregion

    #region Images

    /// <summary>
    ///   The photos fetched with a person within the last two hours.
    /// </summary>
    /// <param name="personID">The TMDb person ID.</param>
    /// <returns>The photos, or <c>null</c> when none were fetched lately.</returns>
    public IReadOnlyList<TmdbImageData>? GetFetchedPersonImages(int personID)
    {
        if (_personImages.TryGetValue(personID, out var fetched) && apiClient.TimeProvider.GetUtcNow() - fetched.FetchedAt < _imagesFreshFor)
            return fetched.Images;

        _personImages.TryRemove(personID, out _);
        return null;
    }

    /// <summary>
    ///   The logo fetched with a company within the last two hours.
    /// </summary>
    /// <param name="companyID">The TMDb company ID.</param>
    /// <param name="logoPath">TMDb's path for the logo, or <c>null</c> when it has none.</param>
    /// <returns>Whether the company was fetched lately.</returns>
    public bool TryGetFetchedCompanyLogo(int companyID, out string? logoPath)
    {
        logoPath = null;
        if (_companyLogos.TryGetValue(companyID, out var fetched) && apiClient.TimeProvider.GetUtcNow() - fetched.FetchedAt < _imagesFreshFor)
        {
            logoPath = fetched.LogoPath;
            return true;
        }

        _companyLogos.TryRemove(companyID, out _);
        return false;
    }

    #endregion
}
