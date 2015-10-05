using JMMModels.Childs;

namespace JMMServerModels.DB
{
    public class Version
    {
        public string Id { get; set; }
        public VersionType Type { get; set; }
        public string Value { get; set; }
    }
}
