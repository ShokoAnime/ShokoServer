using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shoko.Server.PlexAndKodi.Plex
{
    public class PlexDeviceInfo
    {
        public string Device { get; set; }
        public string Product { get; set; }
        public string Version { get; set; }
        public string Platform { get; set; }
        public PlexClient Client { get; set; }

        public PlexDeviceInfo(string device, string product, string version, string platform)
        {
            if ((product != null && product.ToUpperInvariant().Contains("IOS")) ||
                (platform != null && platform.ToUpperInvariant().Contains("IOS")))
                Client = PlexClient.IOS;
            else if ((product != null && product.ToUpperInvariant().Contains("ANDROID")) ||
                     (platform != null && platform.ToUpperInvariant().Contains("ANDROID")))
                Client = PlexClient.Android;
            else if ((product != null && product.ToUpperInvariant().Contains("PLEX MEDIA PLAYER")) ||
                     (platform != null && platform.ToUpperInvariant().Contains("KONVERGO")))
                Client = PlexClient.PlexMediaPlayer;
            else if ((product != null && product.ToUpperInvariant().Contains("KODI")) ||
                     (platform != null && platform.ToUpperInvariant().Contains("KODI")))
                Client = PlexClient.Kodi;
            else if ((product != null && product.ToUpperInvariant().Contains("WINDOWS")))
                Client = PlexClient.PlexForWindows;
            else if ((product != null && product.ToUpperInvariant().Contains("PLEX MEDIA SERVER")))
                Client = PlexClient.PlexMediaPlayer;
            else if ((product != null && product.ToUpperInvariant().Contains("PLEX WEB")))
                Client = PlexClient.Web;
            else if ((device != null && device.ToUpperInvariant().Contains("WEBOS")))
                Client = PlexClient.WebOs;
            else
                Client = PlexClient.Other;
            Device = device;
            Product = product;
            Version = version;
            Platform = platform;
        }

        public override string ToString()
        {
            if (Device == null && Product == null && Version == null && Platform == null)
                return "PRODUCT: Kodi";
            return DoTag("PRODUCT", Product) + " - " + DoTag("DEVICE", Device) + " - " + DoTag("VERSION", Version) +
                   " - " + DoTag("PLATFORM", Platform) + " - " +
                   DoTag("CLIENT", Enum.GetName(typeof(PlexClient), Client));
        }

        private static string DoTag(string name, string value)
        {
            return name + ": " + (string.IsNullOrEmpty(value) ? "NONE" : value);
        }
    }
}