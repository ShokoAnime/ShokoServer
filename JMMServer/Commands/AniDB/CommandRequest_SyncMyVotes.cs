using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Repositories;
using JMMServer.Entities;
using System.Xml;
using AniDBAPI.Commands;
using AniDBAPI;
using JMMDatabase;
using JMMDatabase.Extensions;
using JMMModels;
using JMMModels.Childs;
using JMMServer.AniDB_API.Commands;
using JMMServer.AniDB_API.Raws;
using JMMServerModels.DB.Childs;
using Raven.Client;
using AniDB_Vote = JMMServer.Entities.AniDB_Vote;

namespace JMMServer.Commands
{
	[Serializable]
	public class CommandRequest_SyncMyVotes : BaseCommandRequest, ICommandRequest
	{
		public CommandRequestPriority DefaultPriority 
		{
			get { return CommandRequestPriority.Priority6; }
		}

		public string PrettyDescription
		{
			get
			{
				return "Syncing Vote info from HTTP API";
			}
		}


		public CommandRequest_SyncMyVotes(string userid)
		{
			this.CommandType = CommandRequestType.AniDB_SyncVotes;
			this.Priority = DefaultPriority;
		    this.JMMUserId = userid;
            this.Id= "CommandRequest_SyncMyVotes";
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_SyncMyVotes");

			try
			{

                JMMModels.JMMUser user = Store.JmmUserRepo.Find(JMMUserId).GetUserWithAuth(AuthorizationProvider.AniDB);
                if (user == null)
                    return;

                AniDBAuthorization auth = user.GetAniDBAuthorization();
                AniDBHTTPCommand_GetVotes cmd = new AniDBHTTPCommand_GetVotes();
                cmd.Init(auth.UserName, auth.Password);
                enHelperActivityType ev = cmd.Process();
			    using (IDocumentSession session = Store.GetSession())
			    {
                    List<AnimeSerie> changedSeries=new List<AnimeSerie>();

			        if (ev == enHelperActivityType.GotVotesHTTP)
			        {
			            foreach (Raw_AniDB_Vote_HTTP myVote in cmd.MyVotes)
			            {
                            float val = myVote.VoteValue / 100F;
                            switch (myVote.VoteType)
			                {
			                    case enAniDBVoteType.Anime:
			                    case enAniDBVoteType.AnimeTemp:
			                        AnimeSerie ser = Store.AnimeSerieRepo.AnimeSerieFromAniDBAnime(myVote.EntityID.ToString());
			                        if (ser != null)
			                        {
			                            JMMModels.Childs.AniDB_Vote v = ser.AniDB_Anime.MyVotes.FirstOrDefault(a => a.JMMUserId == user.Id && a.Type == (AniDB_Vote_Type) (int) myVote.VoteType);
			                            if (v == null)
			                            {
			                                v = new JMMModels.Childs.AniDB_Vote { Type= (AniDB_Vote_Type)(int)myVote.VoteType, JMMUserId = user.Id};
			                                ser.AniDB_Anime.MyVotes.Add(v);
			                            }
			                            else if (Math.Abs(v.Vote - val) < float.Epsilon)
			                            {
			                                continue;
			                            }
			                            v.Vote = val;
			                            if (!changedSeries.Contains(ser))
			                                changedSeries.Add(ser);
			                        }
                                    else
                                    {
                                        CommandRequest_GetAnimeHTTP cmdAnime = new CommandRequest_GetAnimeHTTP(myVote.EntityID, false, false);
                                        cmdAnime.Save();
                                    }
                                    break;
                                case enAniDBVoteType.Episode:
			                        AnimeSerie ser2 = Store.AnimeSerieRepo.AnimeSerieFromAniDBEpisode(myVote.EntityID.ToString());
			                        if (ser2 != null)
			                        {
			                            JMMModels.AniDB_Episode ep = ser2.AniDBEpisodeFromAniDB_EpisodeId(myVote.EntityID.ToString());
			                            if (ep != null)
			                            {
                                            JMMModels.Childs.AniDB_Vote v = ep.MyVotes.FirstOrDefault(a => a.JMMUserId == user.Id && a.Type == (AniDB_Vote_Type)(int)myVote.VoteType);
                                            if (v == null)
                                            {
                                                v = new JMMModels.Childs.AniDB_Vote { Type = (AniDB_Vote_Type)(int)myVote.VoteType, JMMUserId = user.Id };
                                                ep.MyVotes.Add(v);
                                            }
                                            else if (Math.Abs(v.Vote - val) < float.Epsilon)
                                            {
                                                continue;
                                            }
                                            v.Vote = val;
                                            if (!changedSeries.Contains(ser2))
                                                changedSeries.Add(ser2);
                                        }
			                        }
			                        break;
                                case enAniDBVoteType.Group:
			                        JMMModels.AniDB_ReleaseGroup grp = Store.ReleaseGroupRepo.Find(myVote.EntityID.ToString());
			                        if (grp != null)
			                        {
                                        JMMModels.Childs.AniDB_Vote v = grp.MyVotes.FirstOrDefault(a => a.JMMUserId == user.Id && a.Type == (AniDB_Vote_Type)(int)myVote.VoteType);
                                        if (v == null)
                                        {
                                            v = new JMMModels.Childs.AniDB_Vote { Type = (AniDB_Vote_Type)(int)myVote.VoteType, JMMUserId = user.Id };
                                            grp.MyVotes.Add(v);
                                        }
                                        else if (Math.Abs(v.Vote - val) < float.Epsilon)
                                        {
                                            continue;
                                        }
                                        Store.ReleaseGroupRepo.Save(grp,session);
                                    }
			                        else
			                        {
			                            CommandRequest_GetReleaseGroup rg=new CommandRequest_GetReleaseGroup(myVote.EntityID,false);
			                            rg.Save();
			                        }
			                        break;

			                }			                
			            }
			            logger.Info("Processed Votes: {0} Items", cmd.MyVotes.Count);
			        }
			    }
			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_SyncMyVotes: {0} ", ex.ToString());

			}
		}
	}
}
