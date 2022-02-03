using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts
{
    [DataContract]
    public class TraktV2CommentShowPost
    {
        [DataMember(Name = "show")]
        public TraktV2ShowPost show { get; set; }

        [DataMember(Name = "comment")]
        public string comment { get; set; }

        [DataMember(Name = "spoiler")]
        public bool spoiler { get; set; }

        public void Init(string shoutText, bool isSpoiler, string traktSlug)
        {
            comment = shoutText;
            spoiler = isSpoiler;
            show = new TraktV2ShowPost
            {
                ids = new TraktV2ShowIdsPost
                {
                    slug = traktSlug
                }
            };
        }
    }
}