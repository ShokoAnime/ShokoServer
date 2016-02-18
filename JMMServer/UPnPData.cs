using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Windows.Forms;
using UPNPLib;

namespace JMMServer
{
    class UPnPData
    {
        /// <summary>
        /// Browses through UPnPService object with id objectId and adds it to a TreeView
        /// </summary>
        /// <param name="service"></param>
        /// <param name="objectId"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        public static XDocument Browser(UPnPService service, string objectId, TreeNode parent)
        {
            object output = new object();
            object[] input = new object[6] { objectId, "BrowseDirectChildren", "", 0, 0, "0", };
            object response;
            Array o;

            response = service.InvokeAction("Browse", input, ref output);
            o = (Array)output;
            XDocument content = XDocument.Parse(o.GetValue(0).ToString());

            return content;
        }

        /// <summary>
        /// Returns array of XML attributes from an XML decendent of type tag
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="XMLDoc"></param>
        /// <param name="attrib"></param>
        /// <returns></returns>
        public static Array buildDecendents(string tag, XDocument XMLDoc, string attrib)
        {
            List<String> ids = new List<String>();
            var a = XMLDoc.Descendants(tag);
            var ar = a.Attributes(attrib).GetEnumerator();
            while (ar.MoveNext())
            {
                ids.Add((ar.Current.Value));
            }
            return ids.ToArray();
        }

        /// <summary>
        /// Adds TreeNode based on Xml.Linq.XElement
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="XML"></param>
        /// <returns></returns>
        public static TreeNode addToTree(TreeNode parent, XElement XML)
        {
            TreeNode child = new TreeNode();
            child.Text = XML.Value.ToString();
            parent.Nodes.Add(child);

            return child;
        }

        /// <summary>
        /// Builds TreeNode structure by recursively reading the XML document made by Browser
        /// </summary>
        /// <param name="s"></param>
        /// <param name="parent"></param>
        /// <param name="XML"></param>
        public static void buildTreeView(UPnPService s, TreeNode parent, XDocument XML)
        {
            string containerTag = "{urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/}container";
            string itemTag = "{urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/}item";
            string resTag = "{urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/}res";
            var items = XML.Descendants(itemTag);
            var p = XML.Descendants("{http://purl.org/dc/elements/1.1/}" + "title");
            var files = XML.Descendants(resTag);
            int i = 0;
            Array containerIds = buildDecendents(containerTag, XML, "id");
            List<String> folders = new List<String>();
            List<XElement> itemlist = new List<XElement>();
            foreach (XElement e in XML.Descendants("{urn:schemas-upnp-org:metadata-1-0/upnp/}class"))
            {
                folders.Add(e.Value);
            }
            if (items.Count() != 0)
            {
                for (int k = 0; k < items.Count(); k++)
                {
                    itemlist.Add((XElement)items.ToArray().GetValue(k));
                    TreeNode child = addToTree(parent, itemlist[k]);
                    child.Text = itemlist[k].Value.ToString();
                    child.Tag = ((XElement)files.ToArray().GetValue(k)).Value;

                }
            }
            else {
                foreach (XElement c in p)
                {

                    if (folders[i] == "object.container.storageFolder")
                    {
                        TreeNode child = addToTree(parent, c);
                        XDocument browsef = Browser(s, containerIds.GetValue(i).ToString(), child);
                        Array childIds = buildDecendents(containerTag, browsef, "id");
                        if (childIds.Length != 0)
                        {
                            buildTreeView(s, child, browsef);
                            for (int j = 0; j < childIds.Length; j++)
                            {
                                TreeNode folder = new TreeNode();
                                browsef = Browser(s, childIds.GetValue(j).ToString(), folder);
                                buildTreeView(s, folder, browsef);
                            }
                        }
                        else
                        {
                            buildTreeView(s, child, browsef);
                        }
                        i++;
                    }
                }
            }
        }
    }
}
