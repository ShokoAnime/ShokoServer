using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Abstractions.Video;
using Shoko.Server.Models.CrossReference.Embedded;
using Shoko.Server.Repositories;

#pragma warning disable CS0618
namespace Shoko.Server.Models.Metadata;

/// <summary>
///   Reads back what the other stores hold for an entry of the series, movie
///   or collection store, so a stored entry reads back whole.
/// </summary>
internal static class MetadataStoredEntry
{
    #region Services

    /// <summary>
    ///   A server service, for the stored rows, which are not built through
    ///   dependency injection.
    /// </summary>
    /// <typeparam name="T">The service.</typeparam>
    /// <returns>The service.</returns>
    private static T Service<T>() where T : notnull
        => ISystemService.StaticServices.GetRequiredService<T>();

    #endregion

    #region Text

    /// <summary>
    ///   The titles an entry lists: its own source's, then what others added.
    ///   An entry with no default title gets a synthesized one first.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The enabled titles, each source's in the order given.</returns>
    internal static IReadOnlyList<ITitle> Titles(IMetadata entry)
        => TextAccess.Manager.ListTitles(entry);

    /// <summary>
    ///   The overviews an entry lists: its own source's, then what others
    ///   added.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The enabled overviews, each source's in the order given.</returns>
    internal static IReadOnlyList<IText> Overviews(IMetadata entry)
        => TextAccess.Manager.ListOverviews(entry);

    /// <summary>
    ///   The title the source calls the entry by: its main title, else the
    ///   synthesized one, else an empty one.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The title.</returns>
    internal static ITitle DefaultTitle(IMetadata entry)
        => TextAccess.Manager.DefaultTitleFor(entry)
            ?? new TitleStub { Source = entry.ID.Source, Language = TitleLanguage.Unknown, LanguageCode = "unk", Value = string.Empty, Type = TitleType.Main };

    /// <summary>
    ///   The title a user picked for the entry, else the one the language
    ///   settings choose, else the synthesized default of an entry with none.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The title, or <c>null</c> when none is picked or in a preferred language and the entry has a default.</returns>
    internal static ITitle? PreferredTitle(IMetadata entry)
        => TextAccess.Manager.PreferredTitleFor(entry) ?? TextAccess.Manager.SynthesizedTitleFor(entry);

    /// <summary>
    ///   The overview the source gives the entry: its first one.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The overview, or <c>null</c> when it has none.</returns>
    internal static IText? DefaultOverview(IMetadata entry)
        => TextAccess.Manager.DefaultOverviewFor(entry);

    /// <summary>
    ///   The overview a user picked for the entry, or else the one the
    ///   language settings choose.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The overview, or <c>null</c> when none is picked or in a preferred language.</returns>
    internal static IText? PreferredOverview(IMetadata entry)
        => TextAccess.Manager.PreferredOverviewFor(entry);

    #endregion

    #region Other Stores

    /// <summary>
    ///   The cast credited on an entry.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The credits, in the order given.</returns>
    internal static IReadOnlyList<ICast> Cast(MetadataGuid entry)
        => RepoFactory.Metadata_Cast.GetByEntry(entry);

    /// <summary>
    ///   The crew credited on an entry.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The credits, in the order given.</returns>
    internal static IReadOnlyList<ICrew> Crew(MetadataGuid entry)
        => RepoFactory.Metadata_Crew.GetByEntry(entry);

    /// <summary>
    ///   The tags on an entry.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The tags, in the order given.</returns>
    internal static IReadOnlyList<ITag> Tags(MetadataGuid entry)
        => Service<IMetadataTagStore>().GetTags(entry);

    /// <summary>
    ///   The studios that worked on an entry.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The studios, in the order given.</returns>
    internal static IReadOnlyList<IStudio> Studios(MetadataGuid entry)
        => Service<IMetadataStudioStore>().GetStudios(entry);

    /// <summary>
    ///   The networks an entry aired on.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The networks, in the order given.</returns>
    internal static IReadOnlyList<INetwork> Networks(MetadataGuid entry)
        => Service<IMetadataStudioStore>().GetNetworks(entry);

    /// <summary>
    ///   The relations from an entry to entries of one kind.
    /// </summary>
    /// <typeparam name="TBase">The entry's own kind.</typeparam>
    /// <typeparam name="TRelated">The kind related to.</typeparam>
    /// <param name="entry">The entry.</param>
    /// <returns>The relations.</returns>
    internal static IReadOnlyList<IRelatedMetadata<TBase, TRelated>> Relations<TBase, TRelated>(MetadataGuid entry)
        where TBase : IMetadata
        where TRelated : IMetadata
        => Service<IMetadataRelationStore>().GetRelations<TBase, TRelated>(entry);

    /// <summary>
    ///   What an entry suggests, among entries of its own kind.
    /// </summary>
    /// <typeparam name="T">The entry's kind.</typeparam>
    /// <param name="entry">The entry.</param>
    /// <returns>The suggestions.</returns>
    internal static IReadOnlyList<ISuggestedMetadata<T, T>> Suggestions<T>(MetadataGuid entry) where T : IMetadata
        => Service<IMetadataSuggestionStore>().GetSuggestions<T, T>(entry);

    /// <summary>
    ///   The suggestions pointing at an entry, from entries of its own kind.
    /// </summary>
    /// <typeparam name="T">The entry's kind.</typeparam>
    /// <param name="entry">The entry.</param>
    /// <returns>The suggestions.</returns>
    internal static IReadOnlyList<ISuggestedMetadata<T, T>> SuggestedBy<T>(MetadataGuid entry) where T : IMetadata
        => Service<IMetadataSuggestionStore>().GetSuggestedBy<T, T>(entry);

    /// <summary>
    ///   The entry's default image of one type: the one its own source pins,
    ///   else the first one the source gave it.
    /// </summary>
    /// <param name="entity">The entry.</param>
    /// <param name="imageType">The type of image.</param>
    /// <returns>The image's cross-reference, or <c>null</c> when there is none.</returns>
    internal static IImageCrossReference? DefaultImage(IWithImages entity, ImageEntityType imageType)
        => DefaultOf(
            entity.GetImageCrossReferences(new() { ImageSource = entity.ID.Source, ImageType = imageType }),
            entity.ID.Source,
            (entity as IMetadataDefaultImageSource)?.GetDefaultResourceID(imageType)
        );

    /// <summary>
    ///   The default among an entry's links of one type: the link to the
    ///   pinned image, else the first link.
    /// </summary>
    /// <param name="xrefs">The links of the type from the entry's source.</param>
    /// <param name="source">The entry's source.</param>
    /// <param name="resourceID">The resource ID of the pinned image, or <c>null</c> for none.</param>
    /// <returns>The link, or <c>null</c> when there is none.</returns>
    internal static IImageCrossReference? DefaultOf(IEnumerable<IImageCrossReference> xrefs, MetadataSource source, string? resourceID)
    {
        var ordered = xrefs.OrderBy(xref => xref.Ordering).ToList();
        if (string.IsNullOrEmpty(resourceID))
            return ordered.FirstOrDefault();

        var imageID = IImageManager.GetIDForImageSourceAndResourceID(source, resourceID);
        return ordered.FirstOrDefault(xref => xref.ImageID == imageID) ?? ordered.FirstOrDefault();
    }

    #endregion

    #region Links

    /// <summary>
    ///   Every link naming an entry.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The links.</returns>
    internal static IReadOnlyList<IMetadataCrossReference> LinksTo(MetadataGuid entry)
        => Service<IMetadataCrossReferenceStore>().GetLinksTo(entry);

    /// <summary>
    ///   The series-level links naming an entry.
    /// </summary>
    /// <param name="entry">The series, or a film claiming a whole anime.</param>
    /// <returns>The links.</returns>
    internal static IReadOnlyList<IMetadataSeriesCrossReference> SeriesLinksTo(MetadataGuid entry)
        => [.. LinksTo(entry).OfType<IMetadataSeriesCrossReference>()];

    /// <summary>
    ///   The episode links naming any of some episodes.
    /// </summary>
    /// <param name="episodes">The episodes.</param>
    /// <returns>The links.</returns>
    internal static IReadOnlyList<IMetadataEpisodeCrossReference> EpisodeLinksTo(IEnumerable<MetadataGuid> episodes)
        => [.. episodes.SelectMany(LinksTo).OfType<IMetadataEpisodeCrossReference>()];

    /// <summary>
    ///   The seasons an entry's episode links point into.
    /// </summary>
    /// <param name="links">The episode links.</param>
    /// <returns>One link per season reached, per anime.</returns>
    internal static IReadOnlyList<IMetadataSeasonCrossReference> SeasonLinks(IEnumerable<IMetadataEpisodeCrossReference> links)
        => [.. links.GroupBy(link => link.AnidbAnimeID).SelectMany(group => MetadataSeasonCrossReference.Project(group.Key, group))];

    /// <summary>
    ///   The Shoko series of the anime some links belong to.
    /// </summary>
    /// <param name="links">The links.</param>
    /// <returns>The series, one per anime.</returns>
    internal static IReadOnlyList<IShokoSeries> ShokoSeries(IEnumerable<IMetadataCrossReference> links)
        => [
            .. links
                .Select(link => link.AnidbAnimeID)
                .Distinct()
                .Select(RepoFactory.AnimeSeries.GetByAnimeID)
                .WhereNotNull(),
        ];

    /// <summary>
    ///   The Shoko episodes of the AniDB episodes some links name.
    /// </summary>
    /// <param name="anidbEpisodeIDs">The AniDB episodes.</param>
    /// <returns>The episodes, one per AniDB episode.</returns>
    internal static IReadOnlyList<IShokoEpisode> ShokoEpisodes(IEnumerable<int> anidbEpisodeIDs)
        => [.. anidbEpisodeIDs.Distinct().Select(RepoFactory.AnimeEpisode.GetByAniDBEpisodeID).WhereNotNull()];

    /// <summary>
    ///   The file links of whole anime.
    /// </summary>
    /// <param name="anidbAnimeIDs">The anime.</param>
    /// <returns>The file links.</returns>
    internal static IReadOnlyList<IVideoCrossReference> VideoLinksForAnime(IEnumerable<int> anidbAnimeIDs)
        => [.. anidbAnimeIDs.Distinct().SelectMany(RepoFactory.CrossRef_File_Episode.GetByAnimeID)];

    /// <summary>
    ///   The file links of some AniDB episodes.
    /// </summary>
    /// <param name="anidbEpisodeIDs">The AniDB episodes.</param>
    /// <returns>The file links.</returns>
    internal static IReadOnlyList<IVideoCrossReference> VideoLinksForEpisodes(IEnumerable<int> anidbEpisodeIDs)
        => [.. anidbEpisodeIDs.Distinct().SelectMany(RepoFactory.CrossRef_File_Episode.GetByEpisodeID)];

    /// <summary>
    ///   The videos some file links point at.
    /// </summary>
    /// <param name="links">The file links.</param>
    /// <returns>The videos, each once.</returns>
    internal static IReadOnlyList<IVideo> Videos(IEnumerable<IVideoCrossReference> links)
        => [.. links.Select(link => link.Video).WhereNotNull().DistinctBy(video => video.ID)];

    #endregion

    #region Comparing

    /// <summary>
    ///   Whether two lists of links hold the same links, in the same order.
    /// </summary>
    /// <param name="first">The first list.</param>
    /// <param name="second">The second list.</param>
    /// <returns><c>true</c> when they are the same.</returns>
    internal static bool SameResources(IReadOnlyList<Resource> first, IReadOnlyList<Resource> second)
        => first.Count == second.Count && first.Zip(second).All(pair =>
            pair.First.Type == pair.Second.Type &&
            pair.First.Name == pair.Second.Name &&
            pair.First.Url == pair.Second.Url &&
            pair.First.ID == pair.Second.ID &&
            pair.First.LanguageCode == pair.Second.LanguageCode);

    #endregion

    #region Events

    /// <summary>
    ///   Where a store's write raises the series, season, episode and movie
    ///   events that <see cref="IMetadataService"/> relays.
    /// </summary>
    internal static ShokoEventHandler Events => ShokoEventHandler.Instance;

    #endregion
}
