using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models;

public class SVR_CrossRef_File_Episode : CrossRef_File_Episode, IVideoCrossReference
{

    public SVR_VideoLocal? VideoLocal => RepoFactory.VideoLocal.GetByEd2k(Hash);

    public SVR_AniDB_Episode? AniDBEpisode => RepoFactory.AniDB_Episode.GetByEpisodeID(EpisodeID);

    public SVR_AnimeEpisode? AnimeEpisode => RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(EpisodeID);

    public SVR_AniDB_Anime? AniDBAnime => AnimeID is 0 ? null : RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);

    public SVR_AnimeSeries? AnimeSeries => AnimeID is 0 ? null : RepoFactory.AnimeSeries.GetByAnimeID(AnimeID);

    public override string ToString() =>
        $"CrossRef_File_Episode (Anime={AnimeID},Episode={EpisodeID},Hash={Hash},FileSize={FileSize},EpisodeOrder={EpisodeOrder},Percentage={Percentage})";

    #region IMetadata implementation

    DataSourceEnum IMetadata.Source => (CrossRefSource)CrossRefSource == Shoko.Models.Enums.CrossRefSource.AniDB
        ? DataSourceEnum.AniDB
        : DataSourceEnum.User;

    #endregion

    #region IVideoCrossReference implementation

    string IVideoCrossReference.ED2K => Hash;

    long IVideoCrossReference.Size => FileSize;

    int IVideoCrossReference.AnidbEpisodeID => EpisodeID;

    int IVideoCrossReference.AnidbAnimeID => AnimeID is 0 ? AniDBEpisode?.AnimeID ?? 0 : AnimeID;

    int IVideoCrossReference.Order => EpisodeOrder;

    int IVideoCrossReference.Percentage => Percentage;

    IVideo? IVideoCrossReference.Video => VideoLocal;

    IEpisode? IVideoCrossReference.AnidbEpisode => AniDBEpisode;

    ISeries? IVideoCrossReference.AnidbAnime => AniDBAnime;

    IShokoEpisode? IVideoCrossReference.ShokoEpisode => AnimeEpisode;

    IShokoSeries? IVideoCrossReference.ShokoSeries => AnimeSeries;

    #endregion
}
