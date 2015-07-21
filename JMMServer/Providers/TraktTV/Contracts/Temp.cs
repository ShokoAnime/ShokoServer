using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace JMMServer.Providers.TraktTV.Contracts
{



    [DataContract]
    public class TraktV2SyncCollectionEpisodes
    {
        [DataMember(Name = "episodes")]
        public List<TraktV2EpisodePost> episodes { get; set; }
    }

    [DataContract]
    public class TraktV2EpisodePost
    {
        [DataMember(Name = "ids")]
        public TraktV2EpisodeIds ids { get; set; }

    }


    [DataContract]
    public class TraktV2EpisodeIdsPost
    {
        [DataMember(Name = "trakt")]
        public int trakt { get; set; }
    }


    [DataContract(Name = "show")]
    public class TraktV2ShowPost
    {
        [DataMember(Name = "ids")]
        public TraktV2ShowIdsPost ids { get; set; }
    }

    [DataContract]
    public class TraktV2CommentShowPost
    {
        [DataMember(Name = "show")]
        public TraktV2ShowPost show { get; set; }

        [DataMember(Name = "comment")]
		public string comment { get; set; }

		[DataMember(Name = "spoiler")]
		public bool spoiler { get; set; }

        public TraktV2CommentShowPost()
		{
		}

		public void Init(string shoutText, bool isSpoiler, string traktSlug)
		{
            comment = shoutText;
			spoiler = isSpoiler;
            show = new TraktV2ShowPost();
            show.ids = new TraktV2ShowIdsPost();
            show.ids.slug = traktSlug;
		}

    }

    [DataContract]
    public class TraktV2ShowIdsPost
    {
        [DataMember(Name = "slug")]
        public string slug { get; set; }
    }
}
