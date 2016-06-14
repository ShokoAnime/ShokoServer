using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;

namespace AniDBAPI.Commands
{
	public interface IAniDBUDPCommand
	{
		enHelperActivityType GetStartEventType();
		enHelperActivityType Process(ref Socket soUDP, ref IPEndPoint remoteIpEndPoint, string sessionID, Encoding enc);
		string GetKey();

	}
}
