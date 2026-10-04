using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Plugin.Tmdb.Api;
using Shoko.Plugin.Tmdb.Mapping;

namespace Shoko.Plugin.Tmdb.Services;

/// <summary>
///   Hands the core the images TMDb has for one of its entities, asking
///   TMDb for them when the image job asks.
/// </summary>
/// <remarks>
///   A show's, season's and episode's are asked for by the numbers the
///   stored entries carry, so only stored entries get any. An episode's
///   stills are its backdrops. The images come in TMDb's order; the ones
///   TMDb names on an entry are stored with it as its defaults on refresh.
/// </remarks>
/// <param name="apiClient">The TMDb client.</param>
/// <param name="stores">The core's stores.</param>
/// <param name="entities">The entity refresh, which keeps the photos and logos it fetched.</param>
public sealed class TmdbImageService(TmdbApiClient apiClient, TmdbStores stores, TmdbEntityRefreshService entities)
{
    /// <summary>
    ///   The images TMDb has for an entity.
    /// </summary>
    /// <param name="entityID">The entity.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The images, in TMDb's order, or <c>null</c> to leave the entity's images alone.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entityID"/> is <c>null</c>.</exception>
    public async Task<IReadOnlyList<ImageCandidate>?> GetImages(MetadataGuid entityID, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entityID);
        if (entityID.Source != MetadataSource.TMDB || !entityID.TryGetNumericID<int>(out var tmdbID) || tmdbID <= 0)
            return null;

        var kind = entityID.EntityType;
        if (kind == MetadataEntityType.Series)
        {
            if (stores.Series.GetSeries(entityID) is null)
                return null;

            var images = await apiClient.GetShowImages(tmdbID, cancellationToken).ConfigureAwait(false);
            return images is null ? null : [.. Posters(images.Posters, images.Logos, images.Backdrops)];
        }

        if (kind == MetadataEntityType.Season)
        {
            if (stores.Series.GetSeason(entityID) is not { } season || !TmdbIds.TryGetID(season.SeriesID, MetadataEntityType.Series, out var showID))
                return null;

            var images = await apiClient.GetSeasonImages(showID, season.SeasonNumber, cancellationToken).ConfigureAwait(false);
            return images is null ? null : [.. TmdbImages.Candidates(images.Posters, ImageEntityType.Primary)];
        }

        if (kind == MetadataEntityType.Episode)
        {
            if (stores.Series.GetEpisode(entityID) is not { SeasonNumber: { } seasonNumber } episode || !TmdbIds.TryGetID(episode.SeriesID, MetadataEntityType.Series, out var showID))
                return null;

            var images = await apiClient.GetEpisodeImages(showID, seasonNumber, episode.EpisodeNumber, cancellationToken).ConfigureAwait(false);
            return images is null ? null : [.. TmdbImages.Candidates(images.Stills, ImageEntityType.Backdrop)];
        }

        if (kind == MetadataEntityType.Movie)
        {
            if (stores.Movies.GetMovie(entityID) is null)
                return null;

            var images = await apiClient.GetMovieImages(tmdbID, cancellationToken).ConfigureAwait(false);
            return images is null ? null : [.. Posters(images.Posters, images.Logos, images.Backdrops)];
        }

        if (kind == MetadataEntityType.Collection)
        {
            if (stores.Collections.GetCollection(entityID) is null)
                return null;

            var images = await apiClient.GetCollectionImages(tmdbID, cancellationToken).ConfigureAwait(false);
            return images is null ? null : [.. Posters(images.Posters, images.Logos, images.Backdrops)];
        }

        if (kind == MetadataEntityType.Creator)
        {
            var profiles = entities.GetFetchedPersonImages(tmdbID)
                ?? (await apiClient.GetPersonImages(tmdbID, cancellationToken).ConfigureAwait(false))?.Profiles;
            return profiles is null ? null : [.. TmdbImages.Candidates(profiles, ImageEntityType.Primary)];
        }

        if (kind == MetadataEntityType.Studio)
        {
            if (!entities.TryGetFetchedCompanyLogo(tmdbID, out var logoPath))
            {
                if (await apiClient.GetCompany(tmdbID, cancellationToken).ConfigureAwait(false) is not { } company)
                    return null;

                logoPath = company.LogoPath;
            }

            return [.. TmdbImages.Single(logoPath, ImageEntityType.Primary)];
        }

        if (kind == MetadataEntityType.Network)
        {
            var logos = await apiClient.GetNetworkImages(tmdbID, cancellationToken).ConfigureAwait(false);
            return logos is null ? null : [.. TmdbImages.Candidates(logos.Logos, ImageEntityType.Primary)];
        }

        return null;
    }

    private static IEnumerable<ImageCandidate> Posters(
        IEnumerable<TMDbLib.Objects.General.ImageData>? posters,
        IEnumerable<TMDbLib.Objects.General.ImageData>? logos,
        IEnumerable<TMDbLib.Objects.General.ImageData>? backdrops
    )
        => TmdbImages.Candidates(posters, ImageEntityType.Primary)
            .Concat(TmdbImages.Candidates(logos, ImageEntityType.Logo))
            .Concat(TmdbImages.Candidates(backdrops, ImageEntityType.Backdrop));
}
