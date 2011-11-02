using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMContracts;

namespace JMMServer.Entities
{
	public class AniDB_Character_Creator
	{
		public int AniDB_Character_CreatorID { get; private set; }
		public int CharID { get; set; }
		public int CreatorID { get; set; }

		public AniDB_Character_Creator()
		{
		}

		public Contract_AniDB_Character_Creator ToContract()
		{
			Contract_AniDB_Character_Creator contract = new Contract_AniDB_Character_Creator();

			contract.AniDB_Character_CreatorID = this.AniDB_Character_CreatorID;
			contract.CharID = this.CharID;
			contract.CreatorID = this.CreatorID;
			

			return contract;
		}
	}
}
