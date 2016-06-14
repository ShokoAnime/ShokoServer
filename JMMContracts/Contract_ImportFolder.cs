using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_ImportFolder
	{
		public int? ImportFolderID { get; set; }
		public int ImportFolderType { get; set; }
		public string ImportFolderName { get; set; }
		public string ImportFolderLocation { get; set; }
		public int IsDropSource { get; set; }
		public int IsDropDestination { get; set; }
		public int IsWatched { get; set; }
	}
}
