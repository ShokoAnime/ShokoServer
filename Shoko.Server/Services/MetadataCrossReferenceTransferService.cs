using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Server.Repositories.Cached.AniDB;

namespace Shoko.Server.Services;

/// <summary>
///   Writes a source's links into a CSV file and reads them back, over the
///   core's cross-reference store.
/// </summary>
/// <param name="crossReferences">The links.</param>
/// <param name="metadataService">Resolves the entries a file names, and the Shoko series.</param>
/// <param name="refreshService">Queues the refresh of what an import links that is not stored yet.</param>
/// <param name="anidbAnime">The AniDB anime, for the comments.</param>
/// <param name="anidbEpisodes">The AniDB episodes, for the comments.</param>
/// <param name="logger">Where an import is reported.</param>
/// <param name="linkChanges">Optional. Reports what an import changed as one change of links.</param>
public class MetadataCrossReferenceTransferService(
    IMetadataCrossReferenceStore crossReferences,
    IMetadataService metadataService,
    IMetadataRefreshService refreshService,
    AniDB_AnimeRepository anidbAnime,
    AniDB_EpisodeRepository anidbEpisodes,
    ILogger<MetadataCrossReferenceTransferService> logger,
    MetadataLinkChangeTracker? linkChanges = null
) : IMetadataCrossReferenceTransferService
{
    private const string MissingTitle = "<missing title>";

    #region Export

    /// <inheritdoc />
    public string Export(MetadataSource source, MetadataCrossReferenceExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        options ??= new();

        var format = new Format(source);
        var sections = options.Sections;
        var output = new StringBuilder();
        if (sections.HasFlag(MetadataCrossReferenceSections.Movie))
            WriteSection(output, format.MovieHeader, $"# AniDB/{source.Name} Movie Cross-References", MovieLines(format, options), options);

        if (options.IncludeComments && sections.HasFlag(MetadataCrossReferenceSections.Movie) &&
            (sections.HasFlag(MetadataCrossReferenceSections.Series) || sections.HasFlag(MetadataCrossReferenceSections.Episode)))
            output.AppendLine().AppendLine();

        if (sections.HasFlag(MetadataCrossReferenceSections.Series))
            WriteSection(output, format.SeriesHeader, $"# AniDB/{source.Name} Show Cross-References", SeriesLines(format, options), options);

        if (options.IncludeComments && sections.HasFlag(MetadataCrossReferenceSections.Episode) && sections.HasFlag(MetadataCrossReferenceSections.Series))
            output.AppendLine().AppendLine();

        if (sections.HasFlag(MetadataCrossReferenceSections.Episode))
            WriteSection(output, format.EpisodeHeader, $"# AniDB/{source.Name} Episode Cross-References", EpisodeLines(format, options), options);

        return output.ToString();
    }

    /// <summary>
    ///   Writes one section, with its header, when it has any lines.
    /// </summary>
    /// <param name="output">Where the file is written.</param>
    /// <param name="header">The section's header.</param>
    /// <param name="title">The comment naming the section.</param>
    /// <param name="lines">The section's lines, comments included.</param>
    /// <param name="options">Whether to write comments.</param>
    private static void WriteSection(StringBuilder output, string header, string title, List<string> lines, MetadataCrossReferenceExportOptions options)
    {
        if (lines.Count is 0)
            return;

        if (options.IncludeComments)
            output.AppendLine("#".PadRight(header.Length, '-')).AppendLine(title);
        output.AppendLine(header);
        if (options.IncludeComments)
            output.AppendLine("#".PadRight(header.Length, '-')).AppendLine();
        foreach (var line in lines)
            output.AppendLine(line);
    }

    /// <summary>
    ///   The film section's lines.
    /// </summary>
    /// <param name="format">The source's file format.</param>
    /// <param name="options">Which links to write.</param>
    /// <returns>The lines, comments included.</returns>
    private List<string> MovieLines(Format format, MetadataCrossReferenceExportOptions options)
        => [
            .. crossReferences.GetAllMovieLinks(format.Source)
                .Where(link => link.ProviderID is not null)
                .Where(link => KeepAutomatic(link, options))
                .Where(link => options.AnidbAnimeID is not { } animeID || animeID == link.AnidbAnimeID)
                .Where(link => options.AnidbEpisodeID is not { } episodeID || episodeID == link.AnidbEpisodeID)
                .Where(link => options.ProviderMovieID is not { } movieID || movieID == format.Write(link.ProviderID))
                // Films are listed by anime, episode and film, so an export
                // compares line for line with an older one.
                .OrderBy(link => link.AnidbAnimeID)
                .ThenBy(link => link.AnidbEpisodeID)
                .ThenBy(link => link.ProviderID!.TryGetNumericID<long>(out var number) ? number : long.MaxValue)
                .ThenBy(link => link.ProviderID!.ID, StringComparer.Ordinal)
                .SelectMany(link =>
                {
                    var entry = Line(link.AnidbAnimeID, link.AnidbEpisodeID, format.Write(link.ProviderID), link.MatchRating);
                    if (!options.IncludeComments)
                        return [entry];

                    var (animeType, animeTitle) = DescribeAnime(link.AnidbAnimeID);
                    var (episodeNumber, episodeTitle) = DescribeAnidbEpisode(link.AnidbEpisodeID);
                    return new[]
                    {
                        string.Empty,
                        $"# AniDB: {animeType} ``{animeTitle}`` (a{link.AnidbAnimeID}) {episodeNumber} (e{link.AnidbEpisodeID}) ``{episodeTitle}`` " +
                        $"(e{link.AnidbEpisodeID}) → {format.Source.Name}: ``{TitleOf(link.ProviderID)}`` (m{format.Write(link.ProviderID)})",
                        entry,
                    };
                }),
        ];

    /// <summary>
    ///   The series section's lines.
    /// </summary>
    /// <param name="format">The source's file format.</param>
    /// <param name="options">Which links to write.</param>
    /// <returns>The lines, comments included.</returns>
    private List<string> SeriesLines(Format format, MetadataCrossReferenceExportOptions options)
        => [
            .. crossReferences.GetAllSeriesLinks(format.Source)
                .Where(link => link.ProviderID is { } providerID && providerID.EntityType == MetadataEntityType.Series)
                .Where(link => KeepAutomatic(link, options))
                .Where(link => options.WithEpisodes is not { } withEpisodes || withEpisodes == crossReferences.GetEpisodeLinksForSeries(link.AnidbAnimeID, format.Source)
                    .Any(episode => episode.ProviderParentID == link.ProviderID && episode.ProviderID is not null))
                .Where(link => options.AnidbAnimeID is not { } animeID || animeID == link.AnidbAnimeID)
                .Where(link => options.ProviderSeriesID is not { } seriesID || seriesID == format.Write(link.ProviderID))
                .SelectMany(link =>
                {
                    var entry = Line(link.AnidbAnimeID, format.Write(link.ProviderID), link.MatchRating);
                    if (!options.IncludeComments)
                        return [entry];

                    var (animeType, animeTitle) = DescribeAnime(link.AnidbAnimeID);
                    return new[]
                    {
                        string.Empty,
                        $"# AniDB: {animeType} ``{animeTitle}`` (a{link.AnidbAnimeID}) → {format.Source.Name}: " +
                        $"``{TitleOf(link.ProviderID)}`` (s{format.Write(link.ProviderID)})",
                        entry,
                    };
                }),
        ];

    /// <summary>
    ///   The episode section's lines.
    /// </summary>
    /// <param name="format">The source's file format.</param>
    /// <param name="options">Which links to write.</param>
    /// <returns>The lines, comments included.</returns>
    private List<string> EpisodeLines(Format format, MetadataCrossReferenceExportOptions options)
        => [
            .. crossReferences.GetAllEpisodeLinks(format.Source)
                .Where(link => KeepAutomatic(link, options))
                .Where(link => options.WithEpisodes is not { } withEpisodes || withEpisodes == link.ProviderID is not null)
                .Where(link => options.AnidbAnimeID is not { } animeID || animeID == link.AnidbAnimeID)
                .Where(link => options.AnidbEpisodeID is not { } episodeID || episodeID == link.AnidbEpisodeID)
                .Where(link => options.ProviderSeriesID is not { } seriesID || seriesID == format.Write(link.ProviderParentID))
                .Where(link => options.ProviderEpisodeID is not { } episodeID || episodeID == format.Write(link.ProviderID))
                .SelectMany(link =>
                {
                    var entry = Line(link.AnidbAnimeID, link.AnidbEpisodeID, format.Write(link.ProviderParentID), format.Write(link.ProviderID), link.MatchRating);
                    if (!options.IncludeComments)
                        return [entry];

                    var (animeType, animeTitle) = DescribeAnime(link.AnidbAnimeID);
                    var (episodeNumber, episodeTitle) = DescribeAnidbEpisode(link.AnidbEpisodeID);
                    var providerEpisode = link.ProviderID is { } episodeID ? metadataService.GetEntry(episodeID) as IEpisode : null;
                    var providerNumber = providerEpisode is null
                        ? "??? ????"
                        : $"S{(providerEpisode.SeasonNumber ?? 0).ToString().PadLeft(2, '0')} E{providerEpisode.EpisodeNumber.ToString().PadLeft(3, '0')}";
                    return new[]
                    {
                        string.Empty,
                        $"# AniDB: {animeType} ``{animeTitle}`` (a{link.AnidbAnimeID}) {episodeNumber} ``{episodeTitle}`` (e{link.AnidbEpisodeID}) → " +
                        $"{format.Source.Name}: ``{TitleOf(link.ProviderParentID)}`` (s{format.Write(link.ProviderParentID)}) {providerNumber} " +
                        $"``{providerEpisode?.DefaultTitle.Value ?? MissingTitle}`` (e{format.Write(link.ProviderID)})",
                        entry,
                    };
                }),
        ];

    /// <summary>
    ///   Whether a link passes the filter on how it was made.
    /// </summary>
    /// <param name="link">The link.</param>
    /// <param name="options">The filter.</param>
    /// <returns><c>true</c> when it is written.</returns>
    private static bool KeepAutomatic(IMetadataCrossReference link, MetadataCrossReferenceExportOptions options)
        => options.Automatic is not { } automatic || automatic == (link.MatchRating is not MatchRating.UserVerified);

    /// <summary>
    ///   An AniDB anime's type code and main title, for a comment.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <returns>The type code and the title.</returns>
    private (string Type, string Title) DescribeAnime(int anidbAnimeID)
        => anidbAnime.GetByAnimeID(anidbAnimeID) is { } anime
            ? (anime.AnimeType switch
            {
                AnimeType.Movie => "MV",
                AnimeType.OVA => "VA",
                AnimeType.TV => "TV",
                AnimeType.TVSpecial => "SP",
                AnimeType.Web => "WB",
                AnimeType.Other => "OT",
                _ => "??",
            }, anime.MainTitle)
            : ("??", MissingTitle);

    /// <summary>
    ///   An AniDB episode's number and title, for a comment.
    /// </summary>
    /// <param name="anidbEpisodeID">The AniDB episode ID.</param>
    /// <returns>The number and the title.</returns>
    private (string Number, string Title) DescribeAnidbEpisode(int anidbEpisodeID)
    {
        if (anidbEpisodes.GetByEpisodeID(anidbEpisodeID) is not { } episode)
            return ("???", MissingTitle);

        var number = episode.EpisodeType is EpisodeType.Episode
            ? episode.EpisodeNumber.ToString().PadLeft(3, '0')
            : $"{episode.EpisodeType.ToString()[0]}{episode.EpisodeNumber.ToString().PadLeft(2, '0')}";
        return (number, episode.DefaultTitle?.Value ?? MissingTitle);
    }

    /// <summary>
    ///   The default title of an entry, for a comment.
    /// </summary>
    /// <param name="entryID">The entry, or <c>null</c> for none.</param>
    /// <returns>The title.</returns>
    private string TitleOf(MetadataGuid? entryID)
        => entryID is not null && metadataService.GetEntry(entryID) is IWithTitles titled ? titled.DefaultTitle.Value : MissingTitle;

    #endregion

    #region Import

    /// <inheritdoc />
    public async Task<MetadataCrossReferenceImportResult> Import(
        MetadataSource source,
        TextReader reader,
        MetadataCrossReferenceImportOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(reader);
        options ??= new();

        var format = new Format(source);
        var file = await Read(format, reader, cancellationToken).ConfigureAwait(false);
        if (file.Errors.Count > 0)
            return new() { Errors = file.Errors };

        var linkCount = file.Movies.Count + file.Series.Count + file.Episodes.Count;
        if (linkCount is 0)
            return new();

        var refreshes = new HashSet<MetadataGuid>();
        var movies = PlanMovies(source, file.Movies, options, refreshes);
        var episodes = PlanEpisodes(source, file.Series, file.Episodes, options, refreshes);

        using (linkChanges?.Begin(MetadataLinkChangeReason.Import))
        {
            if (episodes.SeriesAdding.Count > 0)
                await crossReferences.MergeSeriesLinks(episodes.SeriesAdding, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (movies.Saving.Count > 0 || movies.Removing.Count > 0)
                await crossReferences.MergeMovieLinks(movies.Saving, movies.Removing, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (episodes.Saving.Count > 0 || episodes.Removing.Count > 0)
                await crossReferences.MergeEpisodeLinks(episodes.Saving, episodes.Removing, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        var queued = 0;
        foreach (var entry in refreshes)
            if (await refreshService.RefreshEntry(entry, options: new() { Reason = MetadataRefreshReason.Linked }, cancellationToken: cancellationToken)
                .ConfigureAwait(false))
                queued++;

        var result = new MetadataCrossReferenceImportResult
        {
            LinkCount = linkCount,
            MoviesAdded = movies.Added,
            MoviesUpdated = movies.Saving.Count - movies.Added,
            MoviesKept = movies.Kept,
            MoviesRemoved = movies.Removing.Count,
            SeriesAdded = episodes.SeriesAdding.Count,
            EpisodesAdded = episodes.Added,
            EpisodesUpdated = episodes.Saving.Count - episodes.Added,
            EpisodesKept = episodes.Kept,
            EpisodesRemoved = episodes.Removing.Count,
            RefreshesQueued = queued,
        };
        logger.LogInformation(
            "Imported {Count} {Source} links: {MoviesAdded} film links added, {MoviesUpdated} updated and {MoviesRemoved} removed; {SeriesAdded} series links "
            + "added; {EpisodesAdded} episode links added, {EpisodesUpdated} updated and {EpisodesRemoved} removed; {Refreshes} refreshes queued.",
            linkCount,
            source,
            result.MoviesAdded,
            result.MoviesUpdated,
            result.MoviesRemoved,
            result.SeriesAdded,
            result.EpisodesAdded,
            result.EpisodesUpdated,
            result.EpisodesRemoved,
            queued
        );
        return result;
    }

    /// <summary>
    ///   Works out the film links to add, update and remove.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="lines">The film links in the file.</param>
    /// <param name="options">How to import.</param>
    /// <param name="refreshes">Where the films to refresh are collected.</param>
    /// <returns>The links to save and remove, and how many were new or kept.</returns>
    private (List<MetadataMovieLinkData> Saving, List<IMetadataMovieCrossReference> Removing, int Added, int Kept) PlanMovies(
        MetadataSource source,
        List<MovieLine> lines,
        MetadataCrossReferenceImportOptions options,
        HashSet<MetadataGuid> refreshes
    )
    {
        var existing = crossReferences.GetAllMovieLinks(source)
            .GroupBy(link => (link.AnidbAnimeID, link.AnidbEpisodeID))
            .ToDictionary(group => group.Key, group => group.ToList());
        var saving = new List<MetadataMovieLinkData>();
        var touched = new HashSet<IMetadataMovieCrossReference>();
        var mayRemove = new HashSet<IMetadataMovieCrossReference>();
        var added = 0;
        foreach (var line in lines.DistinctBy(line => (line.AnimeID, line.EpisodeID, line.MovieID)))
        {
            var group = existing.GetValueOrDefault((line.AnimeID, line.EpisodeID)) ?? [];
            var link = group.FirstOrDefault(link => link.ProviderID == line.MovieID);
            if (options.RemoveExisting)
                mayRemove.UnionWith(group);

            if (link is null)
            {
                added++;
                saving.Add(new() { Source = source, AnidbAnimeID = line.AnimeID, AnidbEpisodeID = line.EpisodeID, ProviderID = line.MovieID, MatchRating = line.Rating });
            }
            else
            {
                touched.Add(link);
                if (link.MatchRating is not MatchRating.UserVerified && link.MatchRating != line.Rating)
                    saving.Add(new() { Source = source, AnidbAnimeID = line.AnimeID, AnidbEpisodeID = line.EpisodeID, ProviderID = line.MovieID, MatchRating = line.Rating });
            }

            if (options.AddMissingMovies && metadataService.GetShokoSeriesByAnidbID(line.AnimeID) is not null && metadataService.GetEntry(line.MovieID) is null)
                refreshes.Add(line.MovieID);
        }

        var kept = touched.Count - (saving.Count - added);
        return (saving, [.. mayRemove.Except(touched)], added, kept);
    }

    /// <summary>
    ///   Works out the series links to add, and the episode links to add,
    ///   update and remove.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="seriesLines">The series links in the file.</param>
    /// <param name="episodeLines">The episode links in the file.</param>
    /// <param name="options">How to import.</param>
    /// <param name="refreshes">Where the series to refresh are collected.</param>
    /// <returns>The links to save and remove, and how many episode links were new or kept.</returns>
    private (List<MetadataSeriesLinkData> SeriesAdding, List<MetadataEpisodeLinkData> Saving, List<IMetadataEpisodeCrossReference> Removing, int Added, int Kept)
        PlanEpisodes(
            MetadataSource source,
            List<SeriesLine> seriesLines,
            List<EpisodeLine> episodeLines,
            MetadataCrossReferenceImportOptions options,
            HashSet<MetadataGuid> refreshes
        )
    {
        // A series link the file adds counts as verified by a person.
        var linkedSeries = crossReferences.GetAllSeriesLinks(source)
            .Where(link => link.ProviderID is not null)
            .Select(link => (link.AnidbAnimeID, link.ProviderID!))
            .ToHashSet();
        var seriesAdding = new List<MetadataSeriesLinkData>();
        void LinkSeries(int animeID, MetadataGuid seriesID)
        {
            if (linkedSeries.Add((animeID, seriesID)))
                seriesAdding.Add(new() { Source = source, AnidbAnimeID = animeID, ProviderID = seriesID, MatchRating = MatchRating.UserVerified });
        }

        void RefreshIfMissing(int animeID, MetadataGuid seriesID, MetadataGuid? lookedUp)
        {
            if (options.AddMissingSeries && metadataService.GetShokoSeriesByAnidbID(animeID) is not null && metadataService.GetEntry(lookedUp ?? seriesID) is null)
                refreshes.Add(seriesID);
        }

        // A series line to nothing is skipped: a series is never linked to
        // nothing, and older files hold such lines.
        foreach (var line in seriesLines)
        {
            if (line.SeriesID is not { } seriesID)
                continue;

            LinkSeries(line.AnimeID, seriesID);
            RefreshIfMissing(line.AnimeID, seriesID, null);
        }

        var existing = crossReferences.GetAllEpisodeLinks(source)
            .GroupBy(link => (link.AnidbAnimeID, link.AnidbEpisodeID))
            .ToDictionary(group => group.Key, group => group.ToList());
        var saving = new List<MetadataEpisodeLinkData>();
        var touched = new HashSet<IMetadataEpisodeCrossReference>();
        var mayRemove = new HashSet<IMetadataEpisodeCrossReference>();
        var added = 0;
        foreach (var line in episodeLines.DistinctBy(line => (line.AnimeID, line.AnidbEpisodeID, line.SeriesID, line.EpisodeID)))
        {
            var group = existing.GetValueOrDefault((line.AnimeID, line.AnidbEpisodeID)) ?? [];
            var link = group.FirstOrDefault(link => link.ProviderParentID == line.SeriesID && link.ProviderID == line.EpisodeID);
            if (options.RemoveExisting)
                mayRemove.UnionWith(group);

            // A stored episode's season and numbers are kept with the link, as by hand;
            // a core source's link reads them live, so the store keeps none.
            var episode = line.EpisodeID is { } episodeID && !source.IsCore ? metadataService.GetEpisode(episodeID) : null;
            var data = new MetadataEpisodeLinkData
            {
                Source = source,
                AnidbAnimeID = line.AnimeID,
                AnidbEpisodeID = line.AnidbEpisodeID,
                ProviderID = line.EpisodeID,
                ProviderParentID = line.SeriesID,
                SeasonID = episode?.SeasonID,
                SeasonNumber = episode?.SeasonNumber,
                EpisodeNumber = episode?.EpisodeNumber,
                MatchRating = line.Rating,
            };
            if (link is null)
            {
                added++;
                saving.Add(data);
            }
            else
            {
                touched.Add(link);
                if (link.MatchRating is not MatchRating.UserVerified && link.MatchRating != line.Rating)
                    saving.Add(data);
            }

            if (line.SeriesID is not { } seriesID)
                continue;

            LinkSeries(line.AnimeID, seriesID);
            RefreshIfMissing(line.AnimeID, seriesID, line.EpisodeID);
        }

        var kept = touched.Count - (saving.Count - added);
        return (seriesAdding, saving, [.. mayRemove.Except(touched)], added, kept);
    }

    /// <summary>
    ///   Reads every line of a file, collecting what cannot be read.
    /// </summary>
    /// <remarks>
    ///   A section is told by how many columns its header has, not by their
    ///   names, so a file another tool wrote for the same source reads the
    ///   same: three for series links, four for movie links and five for
    ///   episode links. A header is any line whose first field is not a
    ///   number; every link starts with its AniDB anime ID.
    /// </remarks>
    /// <param name="format">The source's file format.</param>
    /// <param name="reader">The file's text.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The links in each section, and the lines that could not be read.</returns>
    private static async Task<ParsedFile> Read(Format format, TextReader reader, CancellationToken cancellationToken)
    {
        var file = new ParsedFile();
        var section = Section.None;
        var lineNumber = 0;
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            lineNumber++;
            if (line.Length is 0 || line[0] is '#')
                continue;

            var fields = SplitLine(line);
            if (IsHeader(fields))
            {
                section = fields.Count switch
                {
                    3 => Section.Series,
                    4 => Section.Movie,
                    5 => Section.Episode,
                    _ => Section.None,
                };
                if (section is Section.None)
                {
                    file.Errors.Add(new(lineNumber, $"Unknown CSV header with {fields.Count} columns at line {lineNumber}."));
                    break;
                }

                continue;
            }

            switch (section)
            {
                case Section.None:
                    file.Errors.Add(new(lineNumber, "Invalid or missing CSV header for import file."));
                    return file;

                case Section.Movie:
                    if (fields is [var anime, var episode, var movie, var rating] &&
                        TryReadAnidbID(anime, out var animeID) && TryReadAnidbID(episode, out var episodeID) &&
                        format.TryRead(movie, MetadataEntityType.Movie, out var movieID) && movieID is not null &&
                        TryReadRating(rating, out var matchRating))
                        file.Movies.Add(new(animeID, episodeID, movieID, matchRating));
                    else
                        file.Errors.Add(new(lineNumber, $"Unable to parse movie cross-reference at line {lineNumber}."));
                    break;

                case Section.Series:
                    if (fields is [var seriesAnime, var series, var seriesRating] &&
                        TryReadAnidbID(seriesAnime, out var seriesAnimeID) &&
                        format.TryRead(series, MetadataEntityType.Series, out var seriesID) &&
                        TryReadRating(seriesRating, out var seriesMatchRating))
                        file.Series.Add(new(seriesAnimeID, seriesID, seriesMatchRating));
                    else
                        file.Errors.Add(new(lineNumber, $"Unable to parse show cross-reference at line {lineNumber}."));
                    break;

                default:
                    if (fields is [var episodeAnime, var anidbEpisode, var parent, var providerEpisode, var episodeRating] &&
                        TryReadAnidbID(episodeAnime, out var episodeAnimeID) && TryReadAnidbID(anidbEpisode, out var anidbEpisodeID) &&
                        format.TryRead(parent, MetadataEntityType.Series, out var parentID) &&
                        format.TryRead(providerEpisode, MetadataEntityType.Episode, out var providerEpisodeID) &&
                        TryReadRating(episodeRating, out var episodeMatchRating))
                        file.Episodes.Add(new(episodeAnimeID, anidbEpisodeID, parentID, providerEpisodeID, episodeMatchRating));
                    else
                        file.Errors.Add(new(lineNumber, $"Unable to parse episode cross-reference at line {lineNumber}."));
                    break;
            }
        }

        return file;
    }

    /// <summary>
    ///   Whether a line opens a section: its first field is not a number, as
    ///   the AniDB anime ID every link starts with is.
    /// </summary>
    /// <param name="fields">The line's fields.</param>
    /// <returns><c>true</c> for a header.</returns>
    private static bool IsHeader(List<string> fields)
        => fields.Count > 0 && !long.TryParse(fields[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _);

    /// <summary>
    ///   Reads a positive AniDB ID.
    /// </summary>
    /// <param name="text">The field.</param>
    /// <param name="id">The ID read.</param>
    /// <returns><c>true</c> when the field holds one.</returns>
    private static bool TryReadAnidbID(string text, out int id)
        => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out id) && id > 0;

    /// <summary>
    ///   Reads how a link was arrived at, by name, ignoring case.
    /// </summary>
    /// <param name="text">The field.</param>
    /// <param name="rating">The rating read.</param>
    /// <returns><c>true</c> when the field names one.</returns>
    private static bool TryReadRating(string text, out MatchRating rating)
        => Enum.TryParse(text, true, out rating);

    #endregion

    #region CSV

    /// <summary>
    ///   A line of fields, quoted where a field needs it.
    /// </summary>
    /// <param name="fields">The fields.</param>
    /// <returns>The line.</returns>
    private static string Line(params object[] fields)
        => string.Join(',', fields.Select(field => Quote(Convert.ToString(field, CultureInfo.InvariantCulture) ?? string.Empty)));

    /// <summary>
    ///   A field, quoted when it holds a comma, a quote or a line break.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <returns>The field as written.</returns>
    private static string Quote(string field)
        => field.AsSpan().IndexOfAny(",\"\r\n") is -1 ? field : $"\"{field.Replace("\"", "\"\"")}\"";

    /// <summary>
    ///   Splits a line into its fields, reading quoted ones.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <returns>The fields.</returns>
    internal static List<string> SplitLine(string line)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (quoted)
            {
                if (character is '"' && index + 1 < line.Length && line[index + 1] is '"')
                {
                    field.Append('"');
                    index++;
                }
                else if (character is '"')
                {
                    quoted = false;
                }
                else
                {
                    field.Append(character);
                }
            }
            else if (character is '"')
            {
                quoted = true;
            }
            else if (character is ',')
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else
            {
                field.Append(character);
            }
        }

        fields.Add(field.ToString());
        return fields;
    }

    #endregion

    #region Types

    /// <summary>
    ///   How a source's links are written: its headers, and how its IDs and
    ///   a link to nothing are spelt.
    /// </summary>
    private sealed class Format
    {
        /// <summary>
        ///   The format of a source's file, with its headers named after it.
        /// </summary>
        /// <param name="source">The source.</param>
        public Format(MetadataSource source)
        {
            Source = source;
            var prefix = string.Concat(source.Value.Split('-', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
            MovieHeader = $"AnidbAnimeId,AnidbEpisodeId,{prefix}MovieId,Rating";
            SeriesHeader = $"AnidbAnimeId,{prefix}ShowId,Rating";
            EpisodeHeader = $"AnidbAnimeId,AnidbEpisodeId,{prefix}ShowId,{prefix}EpisodeId,Rating";
        }

        /// <summary>
        ///   The source the file is for.
        /// </summary>
        public MetadataSource Source { get; }

        /// <summary>
        ///   The header opening the film section.
        /// </summary>
        public string MovieHeader { get; }

        /// <summary>
        ///   The header opening the series section.
        /// </summary>
        public string SeriesHeader { get; }

        /// <summary>
        ///   The header opening the episode section.
        /// </summary>
        public string EpisodeHeader { get; }

        /// <summary>
        ///   How an entry is written.
        /// </summary>
        /// <param name="id">The entry, or <c>null</c> for none.</param>
        /// <returns>The source's own ID, or how the source writes none.</returns>
        public string Write(MetadataGuid? id)
            => id?.ID ?? string.Empty;

        /// <summary>
        ///   Reads an entry of a kind from a field.
        /// </summary>
        /// <param name="text">The field.</param>
        /// <param name="entityType">The kind of entry.</param>
        /// <param name="id">The entry, or <c>null</c> for none.</param>
        /// <returns><c>true</c> when the field names an entry or none.</returns>
        public bool TryRead(string text, MetadataEntityType entityType, out MetadataGuid? id)
        {
            id = null;

            // A 0 is a link to nothing too, as older files and other tools
            // write it.
            if (text.Length is 0 || text is "0")
                return true;

            if (text.Length > MetadataGuid.MaxIDLength || char.IsWhiteSpace(text[0]) || char.IsWhiteSpace(text[^1]))
                return false;

            id = new(Source, entityType, text);
            return true;
        }
    }

    /// <summary>
    ///   The section of a file a line is in.
    /// </summary>
    private enum Section
    {
        /// <summary>
        ///   Before the first header.
        /// </summary>
        None,

        /// <summary>
        ///   The movie links, four columns.
        /// </summary>
        Movie,

        /// <summary>
        ///   The series links, three columns.
        /// </summary>
        Series,

        /// <summary>
        ///   The episode links, five columns.
        /// </summary>
        Episode,
    }

    /// <summary>
    ///   A film link read from a file.
    /// </summary>
    /// <param name="AnimeID">The AniDB anime.</param>
    /// <param name="EpisodeID">The AniDB episode standing for the film.</param>
    /// <param name="MovieID">The film.</param>
    /// <param name="Rating">How the link was arrived at.</param>
    private sealed record MovieLine(int AnimeID, int EpisodeID, MetadataGuid MovieID, MatchRating Rating);

    /// <summary>
    ///   A series link read from a file.
    /// </summary>
    /// <param name="AnimeID">The AniDB anime.</param>
    /// <param name="SeriesID">The series, or <c>null</c> for none.</param>
    /// <param name="Rating">How the link was arrived at.</param>
    private sealed record SeriesLine(int AnimeID, MetadataGuid? SeriesID, MatchRating Rating);

    /// <summary>
    ///   An episode link read from a file.
    /// </summary>
    /// <param name="AnimeID">The AniDB anime.</param>
    /// <param name="AnidbEpisodeID">The AniDB episode.</param>
    /// <param name="SeriesID">The series the episode sits in, or <c>null</c> for none.</param>
    /// <param name="EpisodeID">The episode, or <c>null</c> for none.</param>
    /// <param name="Rating">How the link was arrived at.</param>
    private sealed record EpisodeLine(int AnimeID, int AnidbEpisodeID, MetadataGuid? SeriesID, MetadataGuid? EpisodeID, MatchRating Rating);

    /// <summary>
    ///   What a file held.
    /// </summary>
    private sealed class ParsedFile
    {
        /// <summary>
        ///   The film section's links.
        /// </summary>
        public List<MovieLine> Movies { get; } = [];

        /// <summary>
        ///   The series section's links.
        /// </summary>
        public List<SeriesLine> Series { get; } = [];

        /// <summary>
        ///   The episode section's links.
        /// </summary>
        public List<EpisodeLine> Episodes { get; } = [];

        /// <summary>
        ///   The lines that could not be read.
        /// </summary>
        public List<MetadataCrossReferenceImportError> Errors { get; } = [];
    }

    #endregion
}
