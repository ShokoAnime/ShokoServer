using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

using File = Shoko.Server.API.v3.Models.Shoko.File;
using FileSource = Shoko.Server.API.v3.Models.Shoko.FileSource;
using GroupSizes = Shoko.Server.API.v3.Models.Shoko.GroupSizes;
using SeriesSizes = Shoko.Server.API.v3.Models.Shoko.SeriesSizes;
using AniDBAnimeType = Shoko.Models.Enums.AnimeType;
using SeriesType = Shoko.Server.API.v3.Models.Shoko.SeriesType;

#nullable enable
namespace Shoko.Server.API.v3.Helpers;

public static class ModelHelper
{
    public static T CombineFlags<T>(this IEnumerable<T> flags) where T : struct, Enum
    {
        T combinedFlags = default;
        foreach (var flag in flags)
            combinedFlags = CombineFlags(combinedFlags, flag);
        return combinedFlags;
    }

    private static T CombineFlags<T>(T a, T b) where T : Enum
        => (T)Enum.ToObject(typeof(T), Convert.ToInt64(a) | Convert.ToInt64(b));

    // Note: there is no `this` because if it's set then the compiler will
    // complain that there is no `System.Enum` defined.
    public static IEnumerable<T> UnCombineFlags<T>(T flags) where T : struct, Enum
    {
        var allValues = Enum.GetValues<T>();
        var flagLong = Convert.ToInt64(flags);
        foreach (var value in allValues)
        {
            var valueLong = Convert.ToInt64(value);
            if (valueLong != 0 && (flagLong & valueLong) == valueLong)
                yield return value;
        }
    }

    public static ListResult<T> ToListResult<T>(this IEnumerable<T> enumerable)
    {
        return new ListResult<T>
        {
            Total = enumerable.Count(),
            List = enumerable.ToList()
        };
    }

    public static ListResult<T> ToListResult<T>(this IEnumerable<T> enumerable, int page, int pageSize)
    {
        if (pageSize <= 0)
        {
            return new ListResult<T>
            {
                Total = enumerable.Count(),
                List = enumerable.ToList()
            };
        }

        return new ListResult<T>
        {
            Total = enumerable.Count(),
            List = enumerable.AsQueryable()
                .Skip(pageSize * (page - 1))
                .Take(pageSize)
                .ToList()
        };
    }

    public static ListResult<U> ToListResult<T, U>(this IEnumerable<T> enumerable, Func<T, U> mapper, int page,
        int pageSize)
    {
        if (pageSize <= 0)
        {
            return new ListResult<U>
            {
                Total = enumerable.Count(),
                List = enumerable
                    .AsParallel()
                    .AsOrdered()
                    .Select(mapper)
                    .ToList()
            };
        }

        return new ListResult<U>
        {
            Total = enumerable.Count(),
            List = enumerable.AsQueryable()
                .Skip(pageSize * (page - 1))
                .Take(pageSize)
                .AsParallel()
                .AsOrdered()
                .Select(mapper)
                .ToList()
        };
    }

    public static ListResult<U> ToListResult<T, U>(this IEnumerable<T> enumerable, Func<T, U> mapper, int total, int page,
        int pageSize)
    {
        if (pageSize <= 0)
        {
            return new ListResult<U>
            {
                Total = total,
                List = enumerable
                    .AsParallel()
                    .AsOrdered()
                    .Select(mapper)
                    .ToList()
            };
        }

        return new ListResult<U>
        {
            Total = total,
            List = enumerable.AsQueryable()
                .Skip(pageSize * (page - 1))
                .Take(pageSize)
                .AsParallel()
                .AsOrdered()
                .Select(mapper)
                .ToList()
        };
    }

    public static SeriesType ToAniDBSeriesType(this int animeType)
        => ToAniDBSeriesType((AniDBAnimeType)animeType);

    public static SeriesType ToAniDBSeriesType(this AniDBAnimeType animeType)
        => animeType switch
        {
            AniDBAnimeType.TVSeries => SeriesType.TV,
            AniDBAnimeType.Movie => SeriesType.Movie,
            AniDBAnimeType.OVA => SeriesType.OVA,
            AniDBAnimeType.TVSpecial => SeriesType.TVSpecial,
            AniDBAnimeType.Web => SeriesType.Web,
            AniDBAnimeType.Other => SeriesType.Other,
            _ => SeriesType.Unknown,
        };

    public static (int, EpisodeType?, string?) GetEpisodeNumberAndTypeFromInput(string input)
    {
        EpisodeType? episodeType = null;
        if (!int.TryParse(input, out var episodeNumber))
        {
            var maybeType = input[0];
            var maybeRangeStart = input.Substring(1);
            if (!int.TryParse(maybeRangeStart, out episodeNumber))
            {
                return (0, null, "Unable to parse an int from `{VariableName}`");
            }

            episodeType = maybeType switch
            {
                'S' => EpisodeType.Special,
                'C' => EpisodeType.Credits,
                'T' => EpisodeType.Trailer,
                'P' => EpisodeType.Parody,
                'O' => EpisodeType.Other,
                'E' => EpisodeType.Episode,
                _ => null
            };
            if (!episodeType.HasValue)
            {
                return (0, null, $"Unknown episode type '{maybeType}' number in `{{VariableName}}`.");
            }
        }

        return (episodeNumber, episodeType, null);
    }

    public static int GetTotalEpisodesForType(IEnumerable<SVR_AnimeEpisode> episodeList, EpisodeType episodeType)
    {
        return episodeList
            .Select(episode => episode.AniDB_Episode)
            .Count(anidbEpisode => anidbEpisode != null && (EpisodeType)anidbEpisode.EpisodeType == episodeType);
    }

    public static string? ToDataURL(byte[] byteArray, string contentType, string fieldName = "ByteArrayToDataUrl", ModelStateDictionary? modelState = null)
    {
        if (byteArray == null || string.IsNullOrEmpty(contentType))
        {
            modelState?.AddModelError(fieldName, $"Invalid byte array or content type for field '{fieldName}'.");
            return null;
        }

        try
        {
            var base64 = Convert.ToBase64String(byteArray);
            return $"data:{contentType};base64,{base64}";
        }
        catch (Exception)
        {
            modelState?.AddModelError(fieldName, $"Unexpected error when converting byte array to data URL for field '{fieldName}'.");
            return null;
        }
    }

    private static readonly string[] _dataUrlSeparators = [":", ";", ","];


    public static (byte[]? byteArray, string? contentType) FromDataURL(string dataUrl, string fieldName = "DataUrlToByteArray", ModelStateDictionary? modelState = null)
    {
        var parts = dataUrl.Split(_dataUrlSeparators, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 || parts[0] != "data")
        {
            modelState?.AddModelError(fieldName, $"Invalid data URL format for field '{fieldName}'.");
            return (null, null);
        }

        try
        {
            var byteArray = Convert.FromBase64String(parts[3]);
            return (byteArray, parts[1]);
        }
        catch (FormatException)
        {
            modelState?.AddModelError(fieldName, $"Base64 data is not in a correct format for field '{fieldName}'.");
            return (null, null);
        }
        catch (Exception)
        {
            modelState?.AddModelError(fieldName, $"Unexpected error when converting data URL to byte array for field '{fieldName}'.");
            return (null, null);
        }
    }

    public static SeriesSizes GenerateSeriesSizes(IEnumerable<SVR_AnimeEpisode> episodeList, int userID)
    {
        var sizes = new SeriesSizes();
        var fileSet = new HashSet<int>();
        foreach (var episode in episodeList)
        {
            var anidbEpisode = episode.AniDB_Episode;
            var fileList = episode.VideoLocals;
            var isLocal = fileList.Count > 0;
            var isWatched = episode.GetUserRecord(userID)?.WatchedDate.HasValue ?? false;
            foreach (var file in fileList)
            {
                // Only iterate the same file once.
                if (!fileSet.Add(file.VideoLocalID))
                    continue;

                var anidbFile = file.AniDBFile;
                if (anidbFile == null)
                {
                    sizes.FileSources.Unknown++;
                    continue;
                }

                switch (File.ParseFileSource(anidbFile.File_Source))
                {
                    case FileSource.Unknown:
                        sizes.FileSources.Unknown++;
                        break;
                    case FileSource.Other:
                        sizes.FileSources.Other++;
                        break;
                    case FileSource.TV:
                        sizes.FileSources.TV++;
                        break;
                    case FileSource.DVD:
                        sizes.FileSources.DVD++;
                        break;
                    case FileSource.BluRay:
                        sizes.FileSources.BluRay++;
                        break;
                    case FileSource.Web:
                        sizes.FileSources.Web++;
                        break;
                    case FileSource.VHS:
                        sizes.FileSources.VHS++;
                        break;
                    case FileSource.VCD:
                        sizes.FileSources.VCD++;
                        break;
                    case FileSource.LaserDisc:
                        sizes.FileSources.LaserDisc++;
                        break;
                    case FileSource.Camera:
                        sizes.FileSources.Camera++;
                        break;
                }
            }

            if (episode.IsHidden)
            {
                sizes.Hidden++;
                continue;
            }

            if (anidbEpisode == null)
            {
                sizes.Total.Unknown++;
                if (isLocal)
                {
                    sizes.Local.Unknown++;

                    if (isWatched)
                    {
                        sizes.Watched.Unknown++;
                    }
                }

                continue;
            }

            switch ((EpisodeType)anidbEpisode.EpisodeType)
            {
                case EpisodeType.Episode:
                    sizes.Total.Episodes++;
                    if (isLocal)
                    {
                        sizes.Local.Episodes++;

                        if (isWatched)
                        {
                            sizes.Watched.Episodes++;
                        }
                    }
                    else if (anidbEpisode.HasAired)
                    {
                        sizes.Missing.Episodes++;
                    }

                    break;
                case EpisodeType.Credits:
                    sizes.Total.Credits++;
                    if (isLocal)
                    {
                        sizes.Local.Credits++;

                        if (isWatched)
                        {
                            sizes.Watched.Credits++;
                        }
                    }

                    break;
                case EpisodeType.Special:
                    sizes.Total.Specials++;
                    if (isLocal)
                    {
                        sizes.Local.Specials++;

                        if (isWatched)
                        {
                            sizes.Watched.Specials++;
                        }
                    }
                    else if (anidbEpisode.HasAired)
                    {
                        sizes.Missing.Specials++;
                    }

                    break;
                case EpisodeType.Trailer:
                    sizes.Total.Trailers++;
                    if (isLocal)
                    {
                        sizes.Local.Trailers++;

                        if (isWatched)
                        {
                            sizes.Watched.Trailers++;
                        }
                    }

                    break;
                case EpisodeType.Parody:
                    sizes.Total.Parodies++;
                    if (isLocal)
                    {
                        sizes.Local.Parodies++;

                        if (isWatched)
                        {
                            sizes.Watched.Parodies++;
                        }
                    }

                    break;
                case EpisodeType.Other:
                    sizes.Total.Others++;
                    if (isLocal)
                    {
                        sizes.Local.Others++;

                        if (isWatched)
                        {
                            sizes.Watched.Others++;
                        }
                    }

                    break;
            }
        }

        return sizes;
    }

    public static GroupSizes GenerateGroupSizes(IEnumerable<SVR_AnimeSeries> seriesList, IEnumerable<SVR_AnimeEpisode> episodeList,
        int subGroups, int userID)
    {
        var sizes = new GroupSizes(GenerateSeriesSizes(episodeList, userID));
        foreach (var series in seriesList)
        {
            var anime = series.AniDB_Anime;
            switch (anime?.AnimeType.ToAniDBSeriesType())
            {
                case SeriesType.Unknown:
                    sizes.SeriesTypes.Unknown++;
                    break;
                case SeriesType.Other:
                    sizes.SeriesTypes.Other++;
                    break;
                case SeriesType.TV:
                    sizes.SeriesTypes.TV++;
                    break;
                case SeriesType.TVSpecial:
                    sizes.SeriesTypes.TVSpecial++;
                    break;
                case SeriesType.Web:
                    sizes.SeriesTypes.Web++;
                    break;
                case SeriesType.Movie:
                    sizes.SeriesTypes.Movie++;
                    break;
                case SeriesType.OVA:
                    sizes.SeriesTypes.OVA++;
                    break;
            }
        }

        sizes.SubGroups = subGroups;
        return sizes;
    }

    public static ListResult<File> FilterFiles(IEnumerable<SVR_VideoLocal> input, SVR_JMMUser user, int pageSize, int page, FileNonDefaultIncludeType[]? include,
        FileExcludeTypes[]? exclude, FileIncludeOnlyType[]? include_only, List<string>? sortOrder, HashSet<DataSource>? includeDataFrom, bool skipSort = false)
    {
        include ??= [];
        exclude ??= [];
        include_only ??= [];

        var includeLocations = exclude.Contains(FileExcludeTypes.Duplicates) ||
            include_only.Contains(FileIncludeOnlyType.Duplicates) ||
            (sortOrder?.Any(criteria => criteria.Contains(File.FileSortCriteria.DuplicateCount.ToString())) ?? false);
        var includeUserRecord = exclude.Contains(FileExcludeTypes.Watched) || (sortOrder?.Any(criteria =>
            criteria.Contains(File.FileSortCriteria.ViewedAt.ToString()) || criteria.Contains(File.FileSortCriteria.WatchedAt.ToString())) ?? false);
        var enumerable = input
            .Select(video => (
                Video: video,
                BestLocation: video.FirstValidPlace,
                Locations: includeLocations ? video.Places : null,
                UserRecord: includeUserRecord ? RepoFactory.VideoLocalUser.GetByUserIDAndVideoLocalID(user.JMMUserID, video.VideoLocalID) : null
            ))
            .Where(tuple =>
            {
                var (video, _, locations, userRecord) = tuple;
                var xrefs = video.EpisodeCrossRefs;
                var isAnimeAllowed = xrefs
                    .Select(xref => xref.AnimeID)
                    .Distinct()
                    .Select(anidbID => RepoFactory.AniDB_Anime.GetByAnimeID(anidbID))
                    .WhereNotNull()
                    .All(user.AllowedAnime);
                if (!isAnimeAllowed)
                    return false;

                // this one is special because ignored files are excluded by default
                if (!include_only.Contains(FileIncludeOnlyType.Ignored) && !include.Contains(FileNonDefaultIncludeType.Ignored) && video.IsIgnored) return false;
                if (include_only.Contains(FileIncludeOnlyType.Ignored) && !video.IsIgnored) return false;

                if (exclude.Contains(FileExcludeTypes.Duplicates) && locations!.Count > 1) return false;
                if (include_only.Contains(FileIncludeOnlyType.Duplicates) && locations!.Count <= 1) return false;

                if (exclude.Contains(FileExcludeTypes.Unrecognized) && xrefs.Count == 0) return false;
                if (include_only.Contains(FileIncludeOnlyType.Unrecognized) && xrefs.Count > 0 && xrefs.Any(x =>
                        RepoFactory.AnimeSeries.GetByAnimeID(x.AnimeID) != null &&
                        RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(x.EpisodeID) != null)) return false;

                if (exclude.Contains(FileExcludeTypes.ManualLinks) && xrefs.Count > 0 &&
                    xrefs.All(xref => xref.CrossRefSource == (int)CrossRefSource.User)) return false;
                if (include_only.Contains(FileIncludeOnlyType.ManualLinks) &&
                    (xrefs.Count == 0 || xrefs.All(xref => xref.CrossRefSource != (int)CrossRefSource.User))) return false;

                if (exclude.Contains(FileExcludeTypes.Watched) && userRecord?.WatchedDate != null) return false;
                if (include_only.Contains(FileIncludeOnlyType.Watched) && userRecord?.WatchedDate == null) return false;

                return true;
            });

        // Sorting.
        if (sortOrder != null && sortOrder.Count > 0)
            enumerable = File.OrderBy(enumerable, sortOrder);
        else if (skipSort)
            enumerable = File.OrderBy(enumerable, new()
            {
                // First sort by import folder from A-Z.
                File.FileSortCriteria.ImportFolderName.ToString(),
                // Then by the relative path inside the import folder, from A-Z.
                File.FileSortCriteria.RelativePath.ToString(),
            });

        // Skip and limit.
        return enumerable.ToListResult(
            tuple => new File(tuple.UserRecord, tuple.Video, include.Contains(FileNonDefaultIncludeType.XRefs), includeDataFrom,
                include.Contains(FileNonDefaultIncludeType.MediaInfo), include.Contains(FileNonDefaultIncludeType.AbsolutePaths)), page, pageSize);
    }
}
