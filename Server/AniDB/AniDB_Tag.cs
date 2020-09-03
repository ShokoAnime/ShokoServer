namespace Shoko.Models.Server
{
    public class AniDB_Tag
    {
        public int AniDB_TagID { get; set; }
        public int TagID { get; set; }
        public int Spoiler { get; set; }
        public int LocalSpoiler { get; set; }
        public int GlobalSpoiler { get; set; }

        public int TagCount { get; set; }

        // Not really pretty, but much prettier than replacing the entire CL_ tree with VMs
        private string tagName;
        public string TagName
        {
            get => tagName == null ? null : string.Intern(tagName);
            set => tagName = value == null ? null : string.Intern(value);
        }

        private string tagDescription;
        public string TagDescription
        {
            get => tagDescription == null ? null : string.Intern(tagDescription);
            set => tagDescription = value == null ? null : string.Intern(value);
        }

        public AniDB_Tag()
        {
        }

        public AniDB_Tag(string tagName)
        {
            TagName = tagName;
        }
    }
}