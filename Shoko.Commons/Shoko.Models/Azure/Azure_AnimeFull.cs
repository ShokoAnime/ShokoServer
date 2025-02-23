using System.Collections.Generic;

namespace Shoko.Models.Azure
{
    public class Azure_AnimeFull
    {
        public Azure_AnimeDetail Detail { get; set; }
        public List<Azure_AnimeCharacter> Characters { get; set; } = new List<Azure_AnimeCharacter>();
        public List<Azure_AnimeComment> Comments { get; set; } = new List<Azure_AnimeComment>();
    }
}