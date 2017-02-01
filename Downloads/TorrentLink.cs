using System;
using System.Globalization;
using System.IO;
using Shoko.Models.Enums;

namespace Shoko.Commons.Downloads
{
    public class TorrentLink
    {
        private string _size;
        private long _sortableSize;

        public TorrentSource Source { get; set; }

        private string torrentName;
        public string TorrentName
        {
            get
            {
                try
                {
                    string dQuote = ((char)34).ToString();
                    string rubish1 = "<span class=" + dQuote + "s" + dQuote + "> </span>";

                    string match = torrentName;
                    match = match.Replace("&#40;", "(");
                    match = match.Replace("&#41;", ")");
                    match = match.Replace("&#39;", "'");
                    match = match.Replace("&#96;", "`");
                    match = match.Replace(rubish1, "");

                    return match;
                }
                catch (Exception ex)
                {
                    return "ERROR";
                }
            }
            set
            {
                RawTorrentName = value;
                torrentName = value;
            }
        }

        public string TorrentDownloadLink { get; set; }
        public string TorrentInfoLink { get; set; }
        public string AnimeType { get; set; }
        public string Size
        {
            get
            {
                return _size;
            }
            set
            {
                _size = value;

                var strSize = _size.Trim();
                var index = strSize.IndexOf(" ");
                var secondPart = "";
                if (index >= 0)
                {
                    secondPart = strSize.Substring(index + 1).Trim();
                    strSize = strSize.Substring(0, index).Trim();
                }
                else
                {
                    index = -1;

                    var charArray = _size.ToCharArray();
                    for (int i = 0; i < charArray.Length; i++)
                    {
                        if (char.IsLetter(_size[i]))
                        {
                            index = i;
                            break;
                        }
                    }

                    if (index != -1)
                    {
                        secondPart = strSize.Substring(index);
                        strSize = strSize.Substring(0, index);
                    }
                }

                double doubleSize = double.NaN;
                if (double.TryParse(strSize, NumberStyles.Any, new CultureInfo("en-US"), out doubleSize) == false)
                    doubleSize = double.NaN;

                if (double.IsNaN(doubleSize) == false)
                {
                    int muliplier = 0;
                    secondPart = secondPart.ToLower();

                    if (secondPart == "pib" || secondPart == "pb")
                        muliplier = 5;
                    else if (secondPart == "tib" || secondPart == "tb")
                        muliplier = 4;
                    else if (secondPart == "gib" || secondPart == "gb")
                        muliplier = 3;
                    else if (secondPart == "mib" || secondPart == "mb")
                        muliplier = 2;
                    else if (secondPart == "kib" || secondPart == "kb")
                        muliplier = 1;

                    for (int i = 0; i < muliplier; i++)
                        doubleSize = doubleSize * 1024;

                    SortableSize = (long)doubleSize;
                }
                else
                    SortableSize = 0;
            }
        }
        public long SortableSize
        {
            get
            {
                return _sortableSize;
            }
            set
            {
                _sortableSize = value;
            }
        }

        public double Seeders { get; set; }
        public double Leechers { get; set; }
        public string RawTorrentName { get; set; }

        public TorrentLink()
        {
        }

        public TorrentLink(TorrentSourceType tpe)
        {
            Source = TorrentSettings.Instance.Create(tpe,true);
        }

        public override string ToString()
        {
            return $"Torrent:   ({Source.TorrentSourceName}) {TorrentName}({RawTorrentName}) - {TorrentDownloadLink} ";
        }

        public string ToStringMatch()
        {
            return $"Torrent Match:   {RawTorrentName} - {ClosestAnimeMatchString} ";
        }

        public string ExtraInfo => $"{AnimeType} ";

        public string TorrentLinkFull
        {
            get
            {
                switch (Source.TorrentSourceType)
                {
                    case TorrentSourceType.BakaBT: return $@"https://bakabt.me/{TorrentInfoLink} ";
                    case TorrentSourceType.AnimeBytes: return $@"http://animebyt.es/{TorrentInfoLink}";
                }
                return TorrentInfoLink;
            }
        }


        public string ClosestAnimeMatchString
        {
            get
            {

                try
                {
                    string match = TorrentName;

                    try { match = Path.GetFileNameWithoutExtension(TorrentName); }
                    catch { }

                    //match = match.Replace("&#40;", "(");
                    //match = match.Replace("&#41;", ")");
                    //match = match.Replace("&#39;", "'");
                    //match = match.Replace("&#96;", "`");

                    //remove any group names or CRC's
                    while (true)
                    {
                        int pos = match.IndexOf('[');
                        if (pos >= 0)
                        {
                            int endPos = match.IndexOf(']', pos);
                            if (endPos >= 0)
                            {
                                string rubbish = match.Substring(pos, endPos - pos + 1);
                                match = match.Replace(rubbish, "");
                            }
                            else break;
                        }
                        else break;
                    }

                    //remove any video information
                    while (true)
                    {
                        int pos = match.IndexOf('(');
                        if (pos >= 0)
                        {
                            int endPos = match.IndexOf(')', pos);
                            if (endPos >= 0)
                            {
                                string rubbish = match.Substring(pos, endPos - pos + 1);
                                match = match.Replace(rubbish, "");
                            }
                            else break;
                        }
                        else break;
                    }

                    match = match.Replace("_", " ");

                    int pos2 = match.IndexOf('-');
                    if (pos2 >= 1)
                    {
                        match = match.Substring(0, pos2).Trim();
                    }

                    return match;
                }
                catch (Exception ex)
                {
                    return "";
                }
            }
        }
    }
}
