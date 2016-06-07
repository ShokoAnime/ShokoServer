using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;
using UPNPLib;

namespace JMMServer
{
    /// <summary>
    ///     Class containing functions for use with UPnP devices
    /// </summary>
    internal class UPnPData
    {
        /// <summary>
        ///     Browses through UPnPService object with id objectId and adds it to a TreeView
        /// </summary>
        /// <param name="service"></param>
        /// <param name="objectId"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        public static XDocument Browser(UPnPService service, string objectId)
        {
            var output = new object();
            var input = new object[6] { objectId, "BrowseDirectChildren", "", 0, 0, "0" };
            object response;
            Array o;

            response = service.InvokeAction("Browse", input, ref output);
            o = (Array)output;
            var content = XDocument.Parse(o.GetValue(0).ToString());

            return content;
        }

        /// <summary>
        ///     Returns array of XML attributes from an XML decendent of type tag
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="XMLDoc"></param>
        /// <param name="attrib"></param>
        /// <returns></returns>
        public static Array buildDecendents(string tag, XDocument XMLDoc, string attrib)
        {
            var ids = new List<string>();
            var a = XMLDoc.Descendants(tag);
            var ar = a.Attributes(attrib).GetEnumerator();
            while (ar.MoveNext())
            {
                ids.Add(ar.Current.Value);
            }
            return ids.ToArray();
        }

        /// <summary>
        ///     Adds TreeNode based on folder name stored in 'o'
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="o"></param>
        /// <returns></returns>
        public static TreeNode addToTree(TreeNode parent, object name, object tag)
        {
            var child = new TreeNode();
            child.Text = name.ToString();
            child.Tag = tag.ToString();
            parent.Nodes.Add(child);

            return child;
        }

        /// <summary>
        ///     Builds TreeNode structure by recursively reading the multidimensional array containing the folder structure
        /// </summary>
        /// <param name="structure"></param>
        /// <param name="parent"></param>
        public static void buildTreeView(object[,] o, TreeNode parent)
        {
            for (var i = 0; i < o.Length / 3; i++)
            {
                var child = addToTree(parent, o[1, i], o[2, i]);
                if (o[0, i] is object[,])
                {
                    buildTreeView((object[,])o[0, i], child);
                }
            }
        }

        /// <summary>
        ///     Generates the folder structure, reading in all the names
        /// </summary>
        /// <param name="s"></param>
        /// <param name="XML"></param>
        /// <returns></returns>
        public static object[,] buildStructure(UPnPService s, XDocument XML)
        {
            var containerTag = "{urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/}container";
            var itemTag = "{urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/}item";
            var resTag = "{urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/}res";
            var titleTag = "{http://purl.org/dc/elements/1.1/}title";

            var items = XML.Descendants(itemTag);
            var containers = XML.Descendants(containerTag);
            var videofile = XML.Descendants(resTag);
            var titles = XML.Descendants(titleTag);

            var containerIds = buildDecendents(containerTag, XML, "id");
            var folders = new List<string>();

            foreach (var e in XML.Descendants("{urn:schemas-upnp-org:metadata-1-0/upnp/}class"))
            {
                folders.Add(e.Value);
            }

            if (items.Count() != 0)
            {
                var i = 0;
                var files = new object[3, items.Count()];
                foreach (var item in items)
                {
                    try
                    {
                        var counter = item.Descendants(titleTag).GetEnumerator();
                        while (counter.MoveNext())
                        {
                            files.SetValue(counter.Current.Value, 1, i);
                        }
                        counter = item.Descendants(resTag).GetEnumerator();
                        while (counter.MoveNext())
                        {
                            files.SetValue(counter.Current.Value, 0, i);
                        }
                        var itemids = buildDecendents(itemTag, item.Document, "id");
                        files.SetValue(itemids.GetValue(i), 2, i);
                        i++;
                    }
                    catch
                    {
                        MessageBox.Show(i + " of " + items.Count());
                    }
                }
                return files;
            }
            else
            {
                var containerFolders = new object[3, folders.Count()];
                var i = 0;
                foreach (var item in containers)
                {
                    if (folders[i] == "object.container.storageFolder")
                    {
                        var browsef = Browser(s, containerIds.GetValue(i).ToString());
                        var childIds = buildDecendents(containerTag, browsef, "id");
                        var counter = titles.GetEnumerator();
                        var titlesarr = new List<string>();
                        while (counter.MoveNext())
                        {
                            titlesarr.Add(counter.Current.Value);
                        }
                        if (childIds.Length != 0)
                        {
                            var childs = new object[3, childIds.Length];
                            var j = 0;
                            var childtitlearr = new List<string>();
                            var childtitles = browsef.Descendants(titleTag).GetEnumerator();
                            while (childtitles.MoveNext())
                            {
                                childtitlearr.Add(childtitles.Current.Value);
                            }
                            foreach (string child in childIds)
                            {
                                browsef = Browser(s, child);
                                var childfolder = buildStructure(s, browsef);
                                childs.SetValue(childfolder, 0, j);
                                childs.SetValue(childtitlearr[j], 1, j);
                                childs.SetValue(child, 2, j);
                                j++;
                            }
                            containerFolders.SetValue(childs, 0, i);
                            containerFolders.SetValue(titlesarr[i], 1, i);
                            containerFolders.SetValue(containerIds.GetValue(i), 2, i);
                        }
                        else
                        {
                            containerFolders.SetValue(buildStructure(s, browsef), 0, i);
                            containerFolders.SetValue(titlesarr[i], 1, i);
                            containerFolders.SetValue(containerIds.GetValue(i), 2, i);
                        }
                        i++;
                    }
                }
                return containerFolders;
            }
        }
    }
}