using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_ImportFolder_SaveResponse
	{
		public string ErrorMessage { get; set; }
		public Contract_ImportFolder ImportFolder { get; set; }
	}
}
