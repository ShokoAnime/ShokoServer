using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_BookmarkedAnime_SaveResponse
	{
		public string ErrorMessage { get; set; }
		public Contract_BookmarkedAnime BookmarkedAnime { get; set; }
	}
}
