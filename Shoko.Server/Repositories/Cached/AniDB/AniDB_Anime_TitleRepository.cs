using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NutzCode.InMemoryIndex;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;

namespace Shoko.Server.Repositories.Cached.AniDB;

public class AniDB_Anime_TitleRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<AniDB_Anime_Title, int>(databaseFactory)
{
    private PocoIndex<int, AniDB_Anime_Title, int>? _animeIDs;

    private Dictionary<int, IReadOnlyList<string>> _normalizedSearchTitles = new();

    protected override int SelectKey(AniDB_Anime_Title entity)
        => entity.AniDB_Anime_TitleID;

    public override void PopulateIndexes()
    {
        _animeIDs = Cache.CreateIndex(a => a.AnimeID);
        _normalizedSearchTitles = Cache.GetAll()
            .Where(t => t.TitleType is TitleType.Main or TitleType.Official)
            .GroupBy(t => t.AnimeID)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(t => NormalizeForSearch(t.Title)).ToList());
    }

    public override void RegenerateDb()
    {
        // Don't need lock in init
        SystemService.StartupMessage = $"Database - Validating - {nameof(AniDB_Anime_Title)} DbRegen...";
        var titles = Cache.GetAll().Where(title => title.Title.Contains('`')).ToList();
        foreach (var title in titles)
        {
            title.Title = title.Title.Replace('`', '\'');
            Save(title);
        }
    }

    protected override void UpdateCacheUnsafe(AniDB_Anime_Title title)
    {
        base.UpdateCacheUnsafe(title);
        RebuildNormalizedForAnime(title.AnimeID);
    }

    protected override void DeleteFromCacheUnsafe(AniDB_Anime_Title title)
    {
        base.DeleteFromCacheUnsafe(title);
        RebuildNormalizedForAnime(title.AnimeID);
    }

    private void RebuildNormalizedForAnime(int animeID)
    {
        var normalized = (_animeIDs?.GetMultiple(animeID) ?? [])
            .Where(t => t.TitleType is TitleType.Main or TitleType.Official)
            .Select(t => NormalizeForSearch(t.Title))
            .ToList();
        if (normalized.Count == 0)
            _normalizedSearchTitles.Remove(animeID);
        else
            _normalizedSearchTitles[animeID] = normalized;
    }

    public static string NormalizeForSearch(string value)
        => value.Normalize(NormalizationForm.FormKC).ToLowerInvariant();

    public bool AnimeMatchesSearch(int animeID, string normalizedQuery)
        => _normalizedSearchTitles.TryGetValue(animeID, out var titles)
            && titles.Any(t => t.Contains(normalizedQuery, StringComparison.Ordinal));

    public List<AniDB_Anime_Title> GetByAnimeID(int animeID)
        => _animeIDs!.GetMultiple(animeID);
}
