using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// Crew member within a season.
/// </summary>
public class TMDB_Show_Crew : TMDB_Crew, ICrew<ISeries>
{
    /// <summary>
    /// TMDB Show ID for the show this job belongs to.
    /// </summary>
    public int TmdbShowID { get; set; }

    /// <inheritdoc/>
    public override int TmdbParentID => TmdbShowID;

    /// <summary>
    /// Number of episodes within this season the crew member have worked on.
    /// </summary>
    public int EpisodeCount { get; set; }

    /// <summary>
    /// Number of season within this show the crew member have worked on.
    /// </summary>
    public int SeasonCount { get; set; }

    public TMDB_Show? GetTmdbShow() => RepoFactory.TMDB_Show.GetByTmdbShowID(TmdbShowID);

    public override IMetadata<int>? GetTmdbParent() => GetTmdbShow();

    ISeries? ICrew<ISeries>.ParentOfType => GetTmdbShow();
}
