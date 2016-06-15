using System.Collections.Generic;

namespace JMMServer.Providers.Azure
{
    public class AnimeFull
    {
        public AnimeDetail Detail { get; set; }
        public List<AnimeCharacter> Characters { get; set; }
        public List<AnimeComment> Comments { get; set; }
    }
}