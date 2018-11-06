using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Commons.Extensions;
using Shoko.Commons.Properties;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.CommandQueue.Commands.Image
{
    public class CmdImageDownloadAllAniDb : BaseCommand<CmdImageDownloadAllAniDb>, ICommand
    {


        public int AnimeID { get; set; }
        public bool ForceDownload { get; set; }


        public string ParallelTag { get; set; } = "ValidateImages";
        public int ParallelMax { get; set; } = 8;
        public int Priority { get; set; } = 1;

        public string Id => $"DownloadAniDBImages_{AnimeID}_{ForceDownload}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct {queueState = QueueStateEnum.DownloadImage, extraParams = new[] {Resources.Command_DownloadAniDBImages, AnimeID.ToString()}};

        public WorkTypes WorkType => WorkTypes.Image;

        public CmdImageDownloadAllAniDb(string str) : base(str)
        {
        }

        public CmdImageDownloadAllAniDb(int animeID, bool forced)
        {
            AnimeID = animeID;
            ForceDownload = forced;
        }


        public override CommandResult Run(IProgress<ICommandProgress> progress = null)
        {
            logger.Info("Processing CommandRequest_DownloadAniDBImages: {0}", AnimeID);
            try
            {
                InitProgress(progress);
                List<ICommand> cmds = new List<ICommand>();
                SVR_AniDB_Anime anime = Repo.Instance.AniDB_Anime.GetByAnimeID(AnimeID);
                UpdateAndReportProgress(progress,25);
                if (anime == null)
                    logger.Warn($"AniDB poster image failed to download: Can't find AniDB_Anime with ID: {AnimeID}");
                else
                    cmds.Add(new CmdImageDownload(AnimeID, ImageEntityType.AniDB_Cover, ForceDownload));

                if (ServerSettings.Instance.AniDb.DownloadCharacters)
                {
                    var chrs = (from xref1 in Repo.Instance.AniDB_Anime_Character.GetByAnimeID(AnimeID) select Repo.Instance.AniDB_Character.GetByCharID(xref1.CharID)).Where(a => !string.IsNullOrEmpty(a?.PicName)).DistinctBy(a => a.CharID).Select(a => a.CharID).ToList();
                    UpdateAndReportProgress(progress,50);
                    if (chrs.Count == 0)
                        logger.Warn($"AniDB Character image failed to download: Can't find Character for anime: {AnimeID}");
                    foreach (var chr in chrs)
                        cmds.Add(new CmdImageDownload(chr, ImageEntityType.AniDB_Character, ForceDownload));
                }

                if (ServerSettings.Instance.AniDb.DownloadCreators)
                {
                    var creators = (from xref1 in Repo.Instance.AniDB_Anime_Character.GetByAnimeID(AnimeID) from xref2 in Repo.Instance.AniDB_Character_Seiyuu.GetByCharID(xref1.CharID) select Repo.Instance.AniDB_Seiyuu.GetBySeiyuuID(xref2.SeiyuuID)).Where(a => !string.IsNullOrEmpty(a?.PicName)).DistinctBy(a => a.SeiyuuID).Select(a => a.SeiyuuID).ToList();
                    UpdateAndReportProgress(progress,75);
                    if (creators.Count == 0)
                        logger.Warn($"AniDB Seiyuu image failed to download: Can't find Seiyuus for anime: {AnimeID}");
                    foreach (var creator in creators)
                        cmds.Add(new CmdImageDownload(creator, ImageEntityType.AniDB_Creator, ForceDownload));
                }

                if (cmds.Count == 0)
                    logger.Warn("Image failed to download: No URLs were generated. This should never happen");
                else
                    Queue.Instance.AddRange(cmds); 
                return ReportFinishAndGetResult(progress);
            }
            catch (Exception ex)
            {
                return ReportErrorAndGetResult(progress, CommandResultStatus.Error, $"Error processing CommandRequest_DownloadAniDBImages: {AnimeID} - {ex}", ex);
            }
        }
    }
}