using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AniDBAPI.Commands
{
    public class AniDBCommand_Vote : AniDBUDPCommand, IAniDBUDPCommand
    {
        public AniDBCommand_Vote()
        {
            commandType = enAniDBCommandType.AddVote;
        }

        public int EntityID { get; set; }

        public int EpisodeNumber { get; set; } = -1;

        public int VoteValue { get; set; }

        public enAniDBVoteType VoteType { get; set; } = enAniDBVoteType.Anime;

        public enEpisodeType EpisodeType { get; set; } = enEpisodeType.Episode;

        public string GetKey()
        {
            return "AniDBCommand_Vote" + EntityID + "_" + EpisodeNumber + "_" + VoteType + "_" + EpisodeType;
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
            if (ResponseCode == 598) return enHelperActivityType.UnknownCommand_598;
            if (ResponseCode == 555) return enHelperActivityType.Banned_555;

            if (errorOccurred) return enHelperActivityType.NoSuchVote;

            var sMsgType = socketResponse.Substring(0, 3);
            switch (sMsgType)
            {
                case "260":
                    return enHelperActivityType.Voted;
                case "261":

                    // this means we were trying to retrieve the vote

                    if (VoteType == enAniDBVoteType.Anime || VoteType == enAniDBVoteType.AnimeTemp)
                    {
                        // 261 VOTE FOUNDCode Geass Hangyaku no Lelouch|900|1|4521
                        var vote = new Raw_AniDB_Vote();
                        vote.ProcessVoteFoundAnime(socketResponse, EntityID, VoteType);
                        VoteValue = vote.VoteValue;
                    }

                    if (VoteType == enAniDBVoteType.Episode)
                    {
                        //261 VOTE FOUNDThe Day a New Demon Was Born|700|1|63091
                        var vote = new Raw_AniDB_Vote();
                        vote.ProcessVoteFoundEpisode(socketResponse, EntityID, EpisodeNumber, EpisodeType);
                        VoteValue = vote.VoteValue;
                    }


                    return enHelperActivityType.VoteFound;
                case "262":
                    return enHelperActivityType.VoteUpdated;
                case "263":
                    return enHelperActivityType.VoteRevoked;
                case "360":
                    return enHelperActivityType.NoSuchVote;
                case "361":
                    return enHelperActivityType.InvalidVoteType;
                case "362":
                    return enHelperActivityType.InvalidVoteValue;
                case "363":
                    return enHelperActivityType.PermVoteNotAllowed;
                case "364":
                    return enHelperActivityType.PermVoteAlready;

                case "501":
                    {
                        return enHelperActivityType.LoginRequired;
                    }
            }

            return enHelperActivityType.NoSuchVote;
        }

        public void Init(int entityid, decimal votevalue, enAniDBVoteType votetype)
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

            EntityID = entityid;
            EpisodeNumber = -1;
            if (votevalue > 0)
                VoteValue = (int)(votevalue * 100);
            else
                VoteValue = (int)votevalue;
            VoteType = votetype;
            EpisodeType = enEpisodeType.Episode;

            commandID = EntityID.ToString();

            var iVoteType = 1;
            switch (VoteType)
            {
                case enAniDBVoteType.Anime:
                    iVoteType = 1;
                    break;
                case enAniDBVoteType.AnimeTemp:
                    iVoteType = 2;
                    break;
                case enAniDBVoteType.Group:
                    iVoteType = 3;
                    break;
                case enAniDBVoteType.Episode:
                    iVoteType = 1;
                    break;
            }

            commandText = "VOTE type=" + iVoteType;
            commandText += "&id=" + EntityID;
            commandText += "&value=" + VoteValue;
        }

        public void InitEpisode(int entityid, int epno, decimal votevalue, enEpisodeType epType)
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

            EntityID = entityid;
            EpisodeNumber = epno;
            if (votevalue > 0)
                VoteValue = (int)(votevalue * 100);
            else
                VoteValue = (int)votevalue;
            VoteType = enAniDBVoteType.Episode;
            EpisodeType = epType;

            commandID = EntityID.ToString();

            var iVoteType = 1;

            var epNumberFormatted = EpisodeNumber.ToString();
            switch (epType)
            {
                case enEpisodeType.Credits:
                    epNumberFormatted = "C" + epno;
                    break;
                case enEpisodeType.Special:
                    epNumberFormatted = "S" + epno;
                    break;
                case enEpisodeType.Other:
                    epNumberFormatted = "0" + epno;
                    break;
                case enEpisodeType.Trailer:
                    epNumberFormatted = "T" + epno;
                    break;
                case enEpisodeType.Parody:
                    epNumberFormatted = "P" + epno;
                    break;
            }

            commandText = "VOTE type=" + iVoteType;
            commandText += "&id=" + EntityID;
            commandText += "&value=" + VoteValue;
            commandText += "&epno=" + epNumberFormatted;
        }
    }
}