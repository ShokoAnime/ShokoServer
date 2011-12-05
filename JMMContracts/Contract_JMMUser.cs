using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_JMMUser
	{
		public int? JMMUserID { get; set; }
		public string Username { get; set; }
		public string Password { get; set; }
		public int IsAdmin { get; set; }
		public int IsAniDBUser { get; set; }
		public int IsTraktUser { get; set; }
		public string HideCategories { get; set; }
		public int? CanEditServerSettings { get; set; }
	}
}
