using System.Net;
using System.Net.Sockets;
using System.Text;
using Shoko.Models.Enums;

namespace AniDBAPI.Commands
{
    public class AniDBCommand_Vote : AniDBUDPCommand, IAniDBUDPCommand
    {
        private int entityID;

        public int EntityID
        {
            get { return entityID; }
            set { entityID = value; }
        }

        private int episodeNumber = -1;

        public int EpisodeNumber
        {
            get { return episodeNumber; }
            set { episodeNumber = value; }
        }

        private int voteValue;

        public int VoteValue
        {
            get { return voteValue; }
            set { voteValue = value; }
        }

        private AniDBVoteType voteType = AniDBVoteType.Anime;

        public AniDBVoteType VoteType
        {
            get { return voteType; }
            set { voteType = value; }
        }

        private EpisodeType episodeType = EpisodeType.Episode;

        public EpisodeType EpisodeType
        {
            get { return episodeType; }
            set { episodeType = value; }
        }

        public string GetKey()
        {
            return "AniDBCommand_Vote" + entityID.ToString() + "_" + episodeNumber.ToString() + "_" +
                   voteType.ToString() + "_" +
                   episodeType.ToString();
        }

        public virtual enHelperActivityType GetStartEventType()
        {
            return enHelperActivityType.AddingVote;
        }

        public virtual enHelperActivityType Process(ref Socket soUDP,
            ref IPEndPoint remoteIpEndPoint, string sessionID, Encoding enc)
        {
            ProcessCommand(ref soUDP, ref remoteIpEndPoint, sessionID, enc);

            // handle 555 BANNED and 598 - UNKNOWN COMMAND
            switch (ResponseCode)
            {
                case 598: return enHelperActivityType.UnknownCommand_598;
                case 555: return enHelperActivityType.Banned_555;
            }

            if (errorOccurred) return enHelperActivityType.NoSuchVote;

            string sMsgType = socketResponse.Substring(0, 3);
            switch (sMsgType)
            {
                case "260": return enHelperActivityType.Voted;
                case "261":

                    // this means we were trying to retrieve the vote
                    if (voteType == AniDBVoteType.Anime || voteType == AniDBVoteType.AnimeTemp)
                    {
                        // 261 VOTE FOUNDCode Geass Hangyaku no Lelouch|900|1|4521
                        Raw_AniDB_Vote vote = new Raw_AniDB_Vote();
                        vote.ProcessVoteFoundAnime(socketResponse, this.entityID, this.voteType);
                        this.voteValue = vote.VoteValue;
                    }
                    if (voteType == AniDBVoteType.Episode)
                    {
                        //261 VOTE FOUNDThe Day a New Demon Was Born|700|1|63091
                        Raw_AniDB_Vote vote = new Raw_AniDB_Vote();
                        vote.ProcessVoteFoundEpisode(socketResponse, this.entityID, this.episodeNumber,
                            this.episodeType);
                        this.voteValue = vote.VoteValue;
                    }
                    return enHelperActivityType.VoteFound;
                case "262": return enHelperActivityType.VoteUpdated;
                case "263": return enHelperActivityType.VoteRevoked;
                    
                case "360": return enHelperActivityType.NoSuchVote;
                case "361": return enHelperActivityType.InvalidVoteType;
                case "362": return enHelperActivityType.InvalidVoteValue;
                case "363": return enHelperActivityType.PermVoteNotAllowed;
                case "364": return enHelperActivityType.PermVoteAlready;

                case "501": return enHelperActivityType.LoginRequired;
            }

            return enHelperActivityType.NoSuchVote;
        }

        public AniDBCommand_Vote()
        {
            commandType = enAniDBCommandType.AddVote;
        }

        public void Init(int entityid, decimal votevalue, AniDBVoteType votetype)
        {
            // allow the user to enter a vote value between 1 and 10
            // can be 9.5 etc
            // then multiple by 100 to get anidb value

            // type: 1=anime, 2=anime tmpvote, 3=group
            // entity: anime, episode, or group
            // for episode voting add epno on type=1
            // value: negative number means revoke, 0 means retrieve (default), 100-1000 are valid vote values, rest is illegal
            // votes will be updated automatically (no questions asked)
            // tmpvoting when there exist a perm vote is not possible

            this.entityID = entityid;
            this.episodeNumber = -1;
            if (votevalue > 0)
                this.voteValue = (int) (votevalue * 100);
            else
                this.voteValue = (int) votevalue;
            this.voteType = votetype;
            this.episodeType = EpisodeType.Episode;

            commandID = entityID.ToString();

            int iVoteType = 1;
            switch (voteType)
            {
                case AniDBVoteType.Anime:
                    iVoteType = 1;
                    break;
                case AniDBVoteType.AnimeTemp:
                    iVoteType = 2;
                    break;
                case AniDBVoteType.Group:
                    iVoteType = 3;
                    break;
                case AniDBVoteType.Episode:
                    iVoteType = 1;
                    break;
            }

            commandText = "VOTE type=" + iVoteType.ToString();
            commandText += "&id=" + entityID.ToString();
            commandText += "&value=" + voteValue.ToString();
        }

        public void InitEpisode(int entityid, int epno, decimal votevalue, EpisodeType epType)
        {
            // allow the user to enter a vote value between 1 and 10
            // can be 9.5 etc
            // then multiple by 100 to get anidb value

            // type: 1=anime, 2=anime tmpvote, 3=group
            // entity: anime, episode, or group
            // for episode voting add epno on type=1
            // value: negative number means revoke, 0 means retrieve (default), 100-1000 are valid vote values, rest is illegal
            // votes will be updated automatically (no questions asked)
            // tmpvoting when there exist a perm vote is not possible

            this.entityID = entityid;
            this.episodeNumber = epno;
            if (votevalue > 0)
                this.voteValue = (int) (votevalue * 100);
            else
                this.voteValue = (int) votevalue;
            this.voteType = AniDBVoteType.Episode;
            this.episodeType = epType;

            commandID = entityID.ToString();

            int iVoteType = 1;

            string epNumberFormatted = episodeNumber.ToString();
            switch (epType)
            {
                case EpisodeType.Credits:
                    epNumberFormatted = "C" + epno.ToString();
                    break;
                case EpisodeType.Special:
                    epNumberFormatted = "S" + epno.ToString();
                    break;
                case EpisodeType.Other:
                    epNumberFormatted = "0" + epno.ToString();
                    break;
                case EpisodeType.Trailer:
                    epNumberFormatted = "T" + epno.ToString();
                    break;
                case EpisodeType.Parody:
                    epNumberFormatted = "P" + epno.ToString();
                    break;
            }

            commandText = "VOTE type=" + iVoteType.ToString();
            commandText += "&id=" + entityID.ToString();
            commandText += "&value=" + voteValue.ToString();
            commandText += "&epno=" + epNumberFormatted;
        }
    }
}