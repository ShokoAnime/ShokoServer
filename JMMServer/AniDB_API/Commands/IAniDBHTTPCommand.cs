using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AniDBAPI.Commands
{
	public interface IAniDBHTTPCommand
	{
		enHelperActivityType GetStartEventType();
		enHelperActivityType Process();
		string GetKey();

	}
}
