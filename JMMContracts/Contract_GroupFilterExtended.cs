using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_GroupFilterExtended
	{
		public Contract_GroupFilter GroupFilter { get; set; }

		public int SeriesCount { get; set; }
		public int GroupCount { get; set; }
	}
}
