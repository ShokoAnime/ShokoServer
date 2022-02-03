using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AniDBAPI.Commands
{
    public interface IAniDBUDPCommand
    {
        AniDBUDPResponseCode GetStartEventType();
        AniDBUDPResponseCode Process(ref Socket soUDP, ref IPEndPoint remoteIpEndPoint, string sessionID, Encoding enc);
        string GetKey();
    }
}