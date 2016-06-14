using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMContracts;

namespace JMMServer.Providers.TvDB
{
	public class TvDBLanguage
	{
		public string Name { get; set; }
		public string Abbreviation { get; set; }

		public Contract_TvDBLanguage ToContract()
		{
			Contract_TvDBLanguage contract = new Contract_TvDBLanguage();

			contract.Abbreviation = this.Abbreviation;
			contract.Name = this.Name;

			return contract;
		}
	}
}
