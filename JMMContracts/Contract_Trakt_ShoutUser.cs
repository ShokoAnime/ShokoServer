using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_Trakt_CommentUser
	{
		// user details
		public Contract_Trakt_User User { get; set; }
        // Comment details
        public Contract_Trakt_Comment Comment { get; set; } 
		
	}
}
