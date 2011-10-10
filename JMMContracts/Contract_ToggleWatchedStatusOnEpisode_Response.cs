using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_ToggleWatchedStatusOnEpisode_Response
	{
		public string ErrorMessage { get; set; }
		public Contract_AnimeEpisode AnimeEpisode { get; set; }
	}
}
