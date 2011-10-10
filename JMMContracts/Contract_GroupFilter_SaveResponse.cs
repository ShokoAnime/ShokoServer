using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_GroupFilter_SaveResponse
	{
		public string ErrorMessage { get; set; }
		public Contract_GroupFilter GroupFilter { get; set; }
	}
}
