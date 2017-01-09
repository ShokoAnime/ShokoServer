using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AniDBAPI.Commands
{
    public interface IAniDBUDPCommand
    {
        enHelperActivityType GetStartEventType();
        enHelperActivityType Process(ref Socket soUDP, ref IPEndPoint remoteIpEndPoint, string sessionID, Encoding enc);
        string GetKey();
    }
}