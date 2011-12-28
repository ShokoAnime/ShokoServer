using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_Trakt_ShoutUser
	{
		// user details
		public Contract_Trakt_User User { get; set; } 
		// shout details
		public Contract_Trakt_Shout Shout { get; set; } 
		
	}
}
