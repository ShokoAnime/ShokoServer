#nullable enable
using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Data.Context;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.Repositories.EntityFramework.TMDB;

public class TMDB_EpisodeRepository
{
    private readonly DataContext _context;

    public TMDB_EpisodeRepository(DataContext context)
    {
        _context = context;
    }

    public IReadOnlyList<TMDB_Episode> GetByTmdbShowID(int showId)
    {
        return _context.Set<TMDB_Episode>()
            .Where(a => a.TmdbShowID == showId)
            .OrderBy(e => e.SeasonNumber == 0)
            .ThenBy(e => e.SeasonNumber)
            .ThenBy(e => e.EpisodeNumber)
            .ToList();
    }

    public IReadOnlyList<TMDB_Episode> GetByTmdbSeasonID(int seasonId)
    {
        return _context.Set<TMDB_Episode>().Where(a => a.TmdbSeasonID == seasonId).OrderBy(a => a.EpisodeNumber).ToList();
    }

    public TMDB_Episode? GetByTmdbEpisodeID(int episodeId)
    {
        return _context.Set<TMDB_Episode>().FirstOrDefault(a => a.TmdbEpisodeID == episodeId);
    }
}
