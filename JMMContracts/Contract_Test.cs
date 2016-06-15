using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace JMMContracts
{
	[DataContract]
	public class Contract_Test
	{
		// from AnimeEpisode
		[DataMember]
		public int AnimeEpisodeID { get; set; }
	}
}
