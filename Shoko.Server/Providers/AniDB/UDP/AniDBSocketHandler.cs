using System;
using System.Net;
using System.Net.Sockets;
using ICSharpCode.SharpZipLib.Zip.Compression;
using NLog;
using Shoko.Server.Providers.AniDB.Interfaces;

namespace Shoko.Server.Providers
{
    public class AniDBSocketHandler : IAniDBSocketHandler
    {
        private IPEndPoint _localIpEndPoint;
        private IPEndPoint _remoteIpEndPoint;
        private Socket _aniDBSocket;
        private string _serverHost;
        private ushort _serverPort;
        private ushort _clientPort;
        private Logger Logger;
        private bool Locked { get; set; }
        public bool IsLocked => Locked;

        public AniDBSocketHandler(string host, ushort serverPort, ushort clientPort)
        {
            Logger = LogManager.GetLogger(nameof(AniDBSocketHandler));
            _aniDBSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _serverHost = host;
            _serverPort = serverPort;
            _clientPort = clientPort;
        }

        public byte[] Send(byte[] payload)
        {
            // this doesn't need to be bigger than 1400, but meh, better safe than sorry
            byte[] result = new byte[1600];
            Locked = true;

            try
            {
                _aniDBSocket.SendTo(payload, _remoteIpEndPoint);
                EndPoint temp = _remoteIpEndPoint;
                int received = _aniDBSocket.ReceiveFrom(result, ref temp);

                if (received > 2 && result[0] == 0 && result[1] == 0)
                {
                    //deflate
                    byte[] buff = new byte[65536];
                    byte[] input = new byte[received - 2];
                    Array.Copy(result, 2, input, 0, received - 2);
                    Inflater inf = new Inflater(false);
                    inf.SetInput(input);
                    inf.Inflate(buff);
                    result = buff;
                    received = (int) inf.TotalOut;
                }

                Array.Resize(ref result, received);
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }

            Locked = false;
            return result;
        }
        
        public bool TryConnection()
        {
            // Dont send Expect 100 requests. These requests aren't always supported by remote internet devices, in which case can cause failure.
            ServicePointManager.Expect100Continue = false;

            try
            {
                _localIpEndPoint = new IPEndPoint(IPAddress.Any, _clientPort);

                _aniDBSocket.Bind(_localIpEndPoint);
                _aniDBSocket.ReceiveTimeout = 30000; // 30 seconds

                Logger.Info("Bound to local address: {0} - Port: {1} ({2})", _localIpEndPoint,
                    _clientPort, _localIpEndPoint.AddressFamily);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Could not bind to local port: {ex}");
                return false;
            }

            try
            {
                IPHostEntry remoteHostEntry = Dns.GetHostEntry(_serverHost);
                _remoteIpEndPoint = new IPEndPoint(remoteHostEntry.AddressList[0], _serverPort);

                Logger.Info($"Bound to remote address: {_remoteIpEndPoint.Address} : {_remoteIpEndPoint.Port}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Could not bind to remote port: {ex}");
                return false;
            }

            return true;
        }


        public void Dispose()
        {
            if (_aniDBSocket == null) return;
            try
            {
                _aniDBSocket.Shutdown(SocketShutdown.Both);
                if (_aniDBSocket.Connected)
                {
                    _aniDBSocket.Disconnect(false);
                }
            }
            catch (SocketException ex)
            {
                Logger.Error($"Failed to Shutdown and Disconnect the connection to AniDB: {ex}");
            }
            finally
            {
                Logger.Info("Closing AniDB Connection...");
                _aniDBSocket.Close();
                Logger.Info("Closed AniDB Connection");
            }
        }
    }
}
