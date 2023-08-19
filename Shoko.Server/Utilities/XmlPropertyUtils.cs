using System.Linq;
using System.Xml;

namespace Shoko.Server.Utilities;

public static class XmlPropertyUtils
{

    public static string TryGetProperty(this XmlDocument doc, string keyName, string propertyName)
    {
        try
        {
            var keyTemp = keyName.ToLowerInvariant().Replace("_", "");
            var parent = doc?.Cast<XmlNode>()
                .FirstOrDefault(a => a.Name.ToLowerInvariant().Replace("_", "").Equals(keyTemp));
            if (parent == null)
            {
                return string.Empty;
            }

            var propName = propertyName.ToLowerInvariant().Replace("_", "");
            var prop = parent.Cast<XmlNode>()
                .FirstOrDefault(a => a.Name.ToLowerInvariant().Replace("_", "").Equals(propName))?.InnerText.Trim();
            return string.IsNullOrEmpty(prop) ? string.Empty : prop;
        }
        catch
        {
            //BaseConfig.MyAnimeLog.Write("---------------------------------------------------------------");
            //BaseConfig.MyAnimeLog.Write("Error in XMLService.TryGetProperty: {0}-{1}", Utils.GetParentMethodName(), ex.ToString());
            //BaseConfig.MyAnimeLog.Write("keyName: {0}, propertyName: {1}", keyName, propertyName);
            //BaseConfig.MyAnimeLog.Write("---------------------------------------------------------------");
        }

        return string.Empty;
    }

    public static string TryGetProperty(this XmlDocument doc, string keyName, params string[] propertyNames)
    {
        try
        {
            var keyTemp = keyName.ToLowerInvariant().Replace("_", "");
            var parent = doc?.Cast<XmlNode>()
                .FirstOrDefault(a => a.Name.ToLowerInvariant().Replace("_", "").Equals(keyTemp));
            if (parent == null)
            {
                return string.Empty;
            }

            foreach (var propertyName in propertyNames)
            {
                var propName = propertyName.ToLowerInvariant().Replace("_", "");
                var prop = parent.Cast<XmlNode>()
                    .FirstOrDefault(a => a.Name.ToLowerInvariant().Replace("_", "").Equals(propName))?.InnerText.Trim();
                if (string.IsNullOrEmpty(prop))
                {
                    continue;
                }

                return prop;
            }
        }
        catch
        {
            //BaseConfig.MyAnimeLog.Write("---------------------------------------------------------------");
            //BaseConfig.MyAnimeLog.Write("Error in XMLService.TryGetProperty: {0}-{1}", Utils.GetParentMethodName(), ex.ToString());
            //BaseConfig.MyAnimeLog.Write("keyName: {0}, propertyName: {1}", keyName, propertyName);
            //BaseConfig.MyAnimeLog.Write("---------------------------------------------------------------");
        }

        return string.Empty;
    }

    public static string TryGetProperty(this XmlDocument doc, string[] keyNames, params string[] propertyNames)
    {
        try
        {
            foreach (var keyName in keyNames)
            {
                var keyTemp = keyName.ToLowerInvariant().Replace("_", "");
                var parent = doc?.Cast<XmlNode>()
                    .FirstOrDefault(a => a.Name.ToLowerInvariant().Replace("_", "").Equals(keyTemp));
                if (parent == null)
                {
                    continue;
                }

                foreach (var propertyName in propertyNames)
                {
                    var propName = propertyName.ToLowerInvariant().Replace("_", "");
                    var prop = parent.Cast<XmlNode>()
                        .FirstOrDefault(a => a.Name.ToLowerInvariant().Replace("_", "").Equals(propName))?.InnerText
                        .Trim();
                    if (string.IsNullOrEmpty(prop))
                    {
                        continue;
                    }

                    return prop;
                }
            }
        }
        catch
        {
            //BaseConfig.MyAnimeLog.Write("---------------------------------------------------------------");
            //BaseConfig.MyAnimeLog.Write("Error in XMLService.TryGetProperty: {0}-{1}", Utils.GetParentMethodName(), ex.ToString());
            //BaseConfig.MyAnimeLog.Write("keyName: {0}, propertyName: {1}", keyName, propertyName);
            //BaseConfig.MyAnimeLog.Write("---------------------------------------------------------------");
        }

        return string.Empty;
    }
}
