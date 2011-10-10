using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMContracts;

namespace JMMServer.Entities
{
	public class ImportFolder
	{
		public int ImportFolderID { get; private set; }
		public int ImportFolderType { get; set; }
		public string ImportFolderName { get; set; }
		public string ImportFolderLocation { get; set; }
		public int IsDropSource { get; set; }
		public int IsDropDestination { get; set; }

		public bool FolderIsDropSource
		{
			get
			{
				return IsDropSource == 1;
			}
		}

		public bool FolderIsDropDestination
		{
			get
			{
				return IsDropDestination == 1;
			}
		}

		public override string ToString()
		{
			return string.Format("{0} - {1} ({2})", ImportFolderName, ImportFolderLocation, ImportFolderID);
		}

		public Contract_ImportFolder ToContract()
		{
			Contract_ImportFolder contract = new Contract_ImportFolder();
			contract.ImportFolderID = this.ImportFolderID;
			contract.ImportFolderType = this.ImportFolderType;
			contract.ImportFolderLocation = this.ImportFolderLocation;
			contract.ImportFolderName = this.ImportFolderName;
			contract.IsDropSource = this.IsDropSource;
			contract.IsDropDestination = this.IsDropDestination;
			return contract;
		}
	}
}
