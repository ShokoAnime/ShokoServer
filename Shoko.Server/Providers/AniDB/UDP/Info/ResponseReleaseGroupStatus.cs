using System;
using System.Collections.Generic;
using System.Linq;

namespace Shoko.Server.Providers.AniDB.UDP.Info
{
    public class ResponseReleaseGroupStatus
    {
        public int AnimeID { get; set; }
        public int GroupID { get; set; }
        public string GroupName { get; set; }
        public Group_CompletionStatus CompletionState { get; set; }
        public int LastEpisodeNumber { get; set; }
        public Decimal Rating { get; set; }
        public int Votes { get; set; }

        /// string because S1 is a thing
        public List<string> ReleasedEpisodes { get; set; }

        public bool HasReleaseGaps
        {
            get
            {
                if (ReleasedEpisodes is not { Count: > 1 }) return false;
                for (var i = 1; i < ReleasedEpisodes.Count; i++)
                {
                    var thisEp = ReleasedEpisodes[i];
                    var prevEp = ReleasedEpisodes[i - 1];
                    var thisChar = thisEp.FirstOrDefault();
                    var prevChar = prevEp.FirstOrDefault();
                    if (thisChar == prevChar && char.IsLetter(thisChar) && char.IsLetter(prevChar))
                    {
                        thisEp = thisEp[1..];
                        prevEp = prevEp[1..];
                    }

                    if (!int.TryParse(thisEp, out var thisEpNum)) continue;
                    if (!int.TryParse(prevEp, out var prevEpNum)) continue;

                    if (thisEpNum - prevEpNum != 1) return true;
                }

                return false;
            }
        }
    }
}
