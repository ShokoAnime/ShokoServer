using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using JMMServer.Entities;
using UPNPLib;

namespace JMMServer
{
    /// <summary>
    ///     Dialog to read DLNA servers on network
    /// </summary>
    public class UPnPServerBrowserDialog
    {
        public static TreeView tvwServerList = new TreeView();
        public static Label lblSearching = new Label();

        public static UPnPService content = new UPnPService();
        public static IUPnPServices services;
        public static List<UPnPDevice> servers = new List<UPnPDevice>();
        public Button btnCancel = new Button();
        public Button btnOk = new Button();
        private readonly UPnPFinderCallback call = new UPnPFinderCallback();
        public UPnPDeviceFinder discovery = new UPnPDeviceFinder();

        public Form frmMainWindow = new Form();
        private ImportFolder importFldr = null;

        /// <summary>
        ///     Form Initialisation
        /// </summary>
        public void Initialise()
        {
            frmMainWindow.Size = new Size(300, 360);
            frmMainWindow.Text = "UPnP Server Browser";

            tvwServerList.Location = new Point(20, 12);
            tvwServerList.Size = new Size(260, 268);
            tvwServerList.Enabled = false;

            btnOk.Location = new Point(115, 285);
            btnOk.Text = "Ok";
            btnOk.Click += btnOk_Click;
            btnCancel.Location = new Point(200, 285);
            btnCancel.Text = "Cancel";
            btnCancel.Click += btnCancel_Click;

            frmMainWindow.Controls.Add(tvwServerList);
            frmMainWindow.Controls.Add(btnOk);
            frmMainWindow.Controls.Add(btnCancel);
            discovery.StartAsyncFind(discovery.CreateAsyncFind("urn:schemas-upnp-org:device:MediaServer:1", 0, call));
        }

        public DialogResult ShowDialog()
        {
            Initialise();
            frmMainWindow.ShowDialog();
            return frmMainWindow.DialogResult;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            frmMainWindow.Close();
            frmMainWindow.DialogResult = DialogResult.Cancel;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            //To-Do: Code to import
            frmMainWindow.DialogResult = DialogResult.OK;
            frmMainWindow.Close();
        }
    }

    /// <summary>
    ///     Callback class to allow population of tree view
    /// </summary>
    internal class UPnPFinderCallback : IUPnPDeviceFinderCallback
    {
        /// <summary>
        ///     when device is found add to tree view
        /// </summary>
        /// <param name="lFindData"></param>
        /// <param name="pDevice"></param>
        public void DeviceAdded(int lFindData, UPnPDevice pDevice)
        {
            var enumerate = pDevice.Services.GetEnumerator();
            var parent = new TreeNode();
            parent.Text = pDevice.FriendlyName;
            UPnPServerBrowserDialog.tvwServerList.Nodes.Add(parent);

            try
            {
                while (enumerate.MoveNext())
                {
                    try
                    {
                        var current = enumerate.Current as UPnPService;
                        var content = UPnPData.Browser(current, "0");
                        var structure = UPnPData.buildStructure(current, content);
                        UPnPData.buildTreeView(structure, parent);
                    }
                    catch
                    {
                        //Do nothing
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
            UPnPServerBrowserDialog.lblSearching.Text = "Finished: " + pDevice.FriendlyName;
        }

        /// <summary>
        ///     When deviced removed, remove from tree view
        /// </summary>
        /// <param name="lFindData"></param>
        /// <param name="bstrUDN"></param>
        public void DeviceRemoved(int lFindData, string bstrUDN)
        {
            var node = new TreeNode();
            foreach (var d in UPnPServerBrowserDialog.servers)
            {
                node.Name = d.FriendlyName;
                if (d.UniqueDeviceName == bstrUDN)
                {
                    UPnPServerBrowserDialog.tvwServerList.Nodes.Remove(node);
                }
            }
        }

        /// <summary>
        ///     When finished searching give user feedback
        /// </summary>
        /// <param name="lFindData"></param>
        public void SearchComplete(int lFindData)
        {
            UPnPServerBrowserDialog.tvwServerList.Enabled = true;
        }
    }
}