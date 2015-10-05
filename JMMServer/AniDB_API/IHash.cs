using System;
using System.Collections.Generic;
using System.Text;

namespace AniDBAPI
{
    public interface IHash
    {
        string ED2KHash { get; set; }
        long FileSize { get; set; }
        string Info { get; }
    }

	public class Hash : IHash
	{
		public long FileSize { get; set; }
		public string ED2KHash { get; set; }
		public string Info { get; set; }
	}
}
