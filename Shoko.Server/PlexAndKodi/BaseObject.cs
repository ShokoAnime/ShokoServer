using System.Collections.Generic;
using System.Linq;
using Shoko.Models.PlexAndKodi;
using Shoko.Server.PlexAndKodi.Plex;

namespace Shoko.Server.PlexAndKodi;

public class BaseObject
{
    public int Start { get; set; }
    public int Size { get; set; }

    public MediaContainer MediaContainer { get; private set; }

    private List<Video> LimitVideos(List<Video> list)
    {
        MediaContainer.TotalSize = list.Count.ToString();
        MediaContainer.Offset = Start.ToString();
        var size = Size > list.Count - Start ? list.Count - Start : Size;
        MediaContainer.TotalSize = list.Count.ToString();
        MediaContainer.Size = size.ToString();
        return list.Skip(Start).Take(size).ToList();
    }

    public List<Video> Childrens
    {
        get => MediaContainer.Childrens;
        set => MediaContainer.Childrens = LimitVideos(value);
    }


    public MediaContainer GetStream(IProvider prov)
    {
        if (MediaContainer.Childrens.Count > 0 && MediaContainer.Childrens[0].Type == "movie")
        {
            MediaContainer.ViewGroup = null;
            MediaContainer.ViewMode = null;
        }

        var isandroid = false;
        var isios = false;
        var dinfo = prov.GetPlexClient();
        if (dinfo != null)
        {
            if (dinfo.Client == PlexClient.Android)
            {
                isandroid = true;
            }
            else if (dinfo.Client == PlexClient.IOS)
            {
                isios = true;
            }
        }

        MediaContainer.Childrens.ForEach(a =>
        {
            a.Group = null;
            if (prov.AddEpisodeNumberToTitlesOnUnsupportedClients && (isios || isandroid) && a.Type == "episode")
            {
                a.Title = a.EpisodeNumber + ". " + a.Title;
            }

            if (isandroid)
            {
                a.Type = null;
            }

            if (prov.RemoveFileAttribute)
            {
                if (a.Medias != null)
                {
                    foreach (var m in a.Medias)
                    {
                        if (m.Parts != null)
                        {
                            foreach (var p in m.Parts)
                            {
                                if (p.Streams != null)
                                {
                                    foreach (var s in p.Streams)
                                    {
                                        s.File = null;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        });
        return MediaContainer;
    }

    public BaseObject(MediaContainer m)
    {
        MediaContainer = m;
    }

    public bool Init(IProvider prov)
    {
        Start = 0;
        Size = int.MaxValue;
        if (prov.HttpContext == null)
        {
            if (prov.HttpContext.Request.Method == "OPTIONS")
            {
                prov.AddResponseHeaders(HttpExtensions.GetOptions(), "text/plain");
                return false;
            }
        }

        var nsize = prov.RequestHeader("X-Plex-Container-Size");
        if (nsize != null)
        {
            if (int.TryParse(nsize, out var max))
            {
                if (max < Size)
                {
                    Size = max;
                }
            }
        }

        var nstart = prov.RequestHeader("X-Plex-Container-Start");
        {
            if (int.TryParse(nstart, out var start))
            {
                Start = start;
            }
        }
        return true;
    }
}
