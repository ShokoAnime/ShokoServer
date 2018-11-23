using System;
using System.Collections.Generic;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Commands;
using Shoko.Server.Providers.AniDB.Raws;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

namespace Shoko.Server.CommandQueue.Commands.AniDB
{
    public class CmdAniDBSyncMyVotes : BaseCommand, ICommand
    {

        public string ParallelTag { get; set; } = WorkTypes.AniDB;
        public int ParallelMax { get; set; } = 1;
        public int Priority { get; set; } = 7;
        public string Id => "SyncMyVotes";
        public string WorkType => WorkTypes.AniDB;


        public QueueStateStruct PrettyDescription => new QueueStateStruct {QueueState = QueueStateEnum.Actions_SyncVotes, ExtraParams = new string[0]};

        // ReSharper disable once UnusedParameter.Local
        public CmdAniDBSyncMyVotes(string _)
        {
        }

        public CmdAniDBSyncMyVotes()
        {
        }
        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info("Processing CommandRequest_SyncMyVotes");
            try
            {
                ReportInit(progress);
                AniDBHTTPCommand_GetVotes cmd = new AniDBHTTPCommand_GetVotes();
                cmd.Init(ServerSettings.Instance.AniDb.Username, ServerSettings.Instance.AniDb.Password);
                ReportUpdate(progress,30);
                enHelperActivityType ev = cmd.Process();
                ReportUpdate(progress,60);
                if (ev == enHelperActivityType.GotVotesHTTP)
                {
                    List<ICommand> cmdstoAdd = new List<ICommand>();
                    foreach (Raw_AniDB_Vote_HTTP myVote in cmd.MyVotes)
                    {
                        List<AniDB_Vote> dbVotes = Repo.Instance.AniDB_Vote.GetByEntity(myVote.EntityID);
                        AniDB_Vote thisVote = null;
                        foreach (AniDB_Vote dbVote in dbVotes)
                        {
                            // we can only have anime permanent or anime temp but not both
                            if (myVote.VoteType == AniDBVoteType.Anime || myVote.VoteType == AniDBVoteType.AnimeTemp)
                            {
                                if (dbVote.VoteType == (int) AniDBVoteType.Anime || dbVote.VoteType == (int) AniDBVoteType.AnimeTemp)
                                {
                                    thisVote = dbVote;
                                }
                            }
                            else
                            {
                                thisVote = dbVote;
                            }
                        }

                        using (var upd = Repo.Instance.AniDB_Vote.BeginAddOrUpdate(thisVote, () => new AniDB_Vote {EntityID = myVote.EntityID}))
                        {
                            upd.Entity.VoteType = (int) myVote.VoteType;
                            upd.Entity.VoteValue = myVote.VoteValue;

                            upd.Commit();
                        }

                        if ((myVote.VoteType == AniDBVoteType.Anime || myVote.VoteType == AniDBVoteType.AnimeTemp) && (thisVote!=null))
                        {
                            cmdstoAdd.Add(new CmdAniDBGetAnimeHTTP(thisVote.EntityID, false, false));
                        }
                    }
                    if (cmdstoAdd.Count>0)
                        Queue.Instance.AddRange(cmdstoAdd);
                    ReportUpdate(progress,90);
                    logger.Info("Processed Votes: {0} Items", cmd.MyVotes.Count);
                }
                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing AniDb.SyncMyVotes: {ex}", ex);
            }
        }
    }
}