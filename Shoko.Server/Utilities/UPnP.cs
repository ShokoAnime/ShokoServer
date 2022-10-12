using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Xml;

namespace UPnP;

public class NAT
{
    private static TimeSpan _timeout = new(0, 0, 0, 3);

    public static TimeSpan TimeOut
    {
        get => _timeout;
        set => _timeout = value;
    }

    private static string _descUrl, _serviceUrl, _eventUrl;

    public static bool Discover()
    {
        var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
        var req = "M-SEARCH * HTTP/1.1\r\n" +
                  "HOST: 239.255.255.250:1900\r\n" +
                  "ST:upnp:rootdevice\r\n" +
                  "MAN:\"ssdp:discover\"\r\n" +
                  "MX:3\r\n\r\n";
        var data = Encoding.ASCII.GetBytes(req);
        var ipe = new IPEndPoint(IPAddress.Broadcast, 1900);
        var buffer = new byte[0x1000];

        var start = DateTime.Now;

        do
        {
            s.SendTo(data, ipe);
            s.SendTo(data, ipe);
            s.SendTo(data, ipe);

            var length = 0;
            do
            {
                length = s.Receive(buffer);

                var resp = Encoding.ASCII.GetString(buffer, 0, length).ToLower();
                if (resp.Contains("upnp:rootdevice"))
                {
                    resp = resp.Substring(resp.ToLower().IndexOf("location:") + 9);
                    resp = resp.Substring(0, resp.IndexOf("\r")).Trim();
                    if (!string.IsNullOrEmpty(_serviceUrl = GetServiceUrl(resp)))
                    {
                        _descUrl = resp;
                        return true;
                    }
                }
            } while (length > 0);
        } while (start.Subtract(DateTime.Now) < _timeout);

        return false;
    }

    private static string GetServiceUrl(string resp)
    {
#if !DEBUG
            try
            {
#endif
        var desc = new XmlDocument();
        desc.Load(WebRequest.Create(resp).GetResponse().GetResponseStream());
        var nsMgr = new XmlNamespaceManager(desc.NameTable);
        nsMgr.AddNamespace("tns", "urn:schemas-upnp-org:device-1-0");
        var typen = desc.SelectSingleNode("//tns:device/tns:deviceType/text()", nsMgr);
        if (!typen.Value.Contains("InternetGatewayDevice"))
        {
            return null;
        }

        var node =
            desc.SelectSingleNode(
                "//tns:service[tns:serviceType=\"urn:schemas-upnp-org:service:WANIPConnection:1\"]/tns:controlURL/text()",
                nsMgr);
        if (node == null)
        {
            return null;
        }

        var eventnode =
            desc.SelectSingleNode(
                "//tns:service[tns:serviceType=\"urn:schemas-upnp-org:service:WANIPConnection:1\"]/tns:eventSubURL/text()",
                nsMgr);
        _eventUrl = CombineUrls(resp, eventnode.Value);
        return CombineUrls(resp, node.Value);
#if !DEBUG
            }
            catch { return null; }
#endif
    }

    private static string CombineUrls(string resp, string p)
    {
        var n = resp.IndexOf("://");
        n = resp.IndexOf('/', n + 3);
        return resp.Substring(0, n) + p;
    }

    public static void ForwardPort(int port, ProtocolType protocol, string description)
    {
        if (string.IsNullOrEmpty(_serviceUrl))
        {
            throw new Exception("No UPnP service available or Discover() has not been called");
        }

        var xdoc = SOAPRequest(_serviceUrl,
            "<u:AddPortMapping xmlns:u=\"urn:schemas-upnp-org:service:WANIPConnection:1\">" +
            "<NewRemoteHost></NewRemoteHost><NewExternalPort>" + port +
            "</NewExternalPort><NewProtocol>" +
            protocol.ToString().ToUpper() + "</NewProtocol>" +
            "<NewInternalPort>" + port + "</NewInternalPort><NewInternalClient>" +
            Dns.GetHostAddresses(Dns.GetHostName())[0] +
            "</NewInternalClient><NewEnabled>1</NewEnabled><NewPortMappingDescription>" + description +
            "</NewPortMappingDescription><NewLeaseDuration>0</NewLeaseDuration></u:AddPortMapping>",
            "AddPortMapping");
    }

    public static void DeleteForwardingRule(int port, ProtocolType protocol)
    {
        if (string.IsNullOrEmpty(_serviceUrl))
        {
            throw new Exception("No UPnP service available or Discover() has not been called");
        }

        var xdoc = SOAPRequest(_serviceUrl,
            "<u:DeletePortMapping xmlns:u=\"urn:schemas-upnp-org:service:WANIPConnection:1\">" +
            "<NewRemoteHost>" +
            "</NewRemoteHost>" +
            "<NewExternalPort>" + port + "</NewExternalPort>" +
            "<NewProtocol>" + protocol.ToString().ToUpper() + "</NewProtocol>" +
            "</u:DeletePortMapping>", "DeletePortMapping");
    }

    public static IPAddress GetExternalIP()
    {
        if (string.IsNullOrEmpty(_serviceUrl))
        {
            throw new Exception("No UPnP service available or Discover() has not been called");
        }

        var xdoc = SOAPRequest(_serviceUrl,
            "<u:GetExternalIPAddress xmlns:u=\"urn:schemas-upnp-org:service:WANIPConnection:1\">" +
            "</u:GetExternalIPAddress>", "GetExternalIPAddress");
        var nsMgr = new XmlNamespaceManager(xdoc.NameTable);
        nsMgr.AddNamespace("tns", "urn:schemas-upnp-org:device-1-0");
        var IP = xdoc.SelectSingleNode("//NewExternalIPAddress/text()", nsMgr).Value;
        return IPAddress.Parse(IP);
    }

    private static XmlDocument SOAPRequest(string url, string soap, string function)
    {
        var req = "<?xml version=\"1.0\"?>" +
                  "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">" +
                  "<s:Body>" +
                  soap +
                  "</s:Body>" +
                  "</s:Envelope>";
        var r = WebRequest.Create(url);
        r.Method = "POST";
        var b = Encoding.UTF8.GetBytes(req);
        r.Headers.Add("SOAPACTION", "\"urn:schemas-upnp-org:service:WANIPConnection:1#" + function + "\"");
        r.ContentType = "text/xml; charset=\"utf-8\"";
        r.ContentLength = b.Length;
        r.GetRequestStream().Write(b, 0, b.Length);
        var resp = new XmlDocument();
        var wres = r.GetResponse();
        var ress = wres.GetResponseStream();
        resp.Load(ress);
        return resp;
    }

    public static bool UPnPJMMFilePort(int jmmfileport)
    {
        try
        {
            if (Discover())
            {
                ForwardPort(jmmfileport, ProtocolType.Tcp, "JMM File Port");
                UPnPPortAvailable = true;
            }
            else
            {
                UPnPPortAvailable = false;
            }
        }
        catch (Exception)
        {
            UPnPPortAvailable = false;
        }

        return UPnPPortAvailable;
    }

    public static bool UPnPPortAvailable { get; private set; }
    private static IPAddress CachedAddress;
    private static DateTime LastChange = DateTime.MinValue;
    private static bool IPThreadLock;
    private static bool IPFirstTime;

    public static IPAddress GetExternalAddress()
    {
        try
        {
            if (LastChange < DateTime.Now)
            {
                if (IPFirstTime)
                {
                    IPFirstTime = false;
                    CachedAddress = GetExternalIP();
                }
                else if (!IPThreadLock)
                {
                    IPThreadLock = true;
                    LastChange = DateTime.Now.AddMinutes(2);
                    ThreadPool.QueueUserWorkItem(a =>
                    {
                        CachedAddress = GetExternalIP();
                        IPThreadLock = false;
                    });
                }
            }
        }
        catch (Exception)
        {
            return null;
        }

        return CachedAddress;
    }
}
