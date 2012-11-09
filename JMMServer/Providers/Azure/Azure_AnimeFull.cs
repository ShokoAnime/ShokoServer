using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMServer.Providers.Azure
{
	public class AnimeFull
	{
		public AnimeDetail Detail { get; set; }
		public List<AnimeCharacter> Characters { get; set; }
		public List<AnimeShout> Shouts { get; set; }
	}

	
}
