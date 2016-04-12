using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Xml.Linq;
using UPNPLib;
using JMMContracts;
using JMMFileHelper;
using JMMServer.Entities;

namespace JMMServer
{
    /// <summary>
    /// Dialog to read DLNA servers on network
    /// </summary>
    public partial class UPnPServerBrowserDialog
    {
        private ImportFolder importFldr = null;

        public Form frmMainWindow = new Form();

        public static TreeView tvwServerList = new TreeView();
        public static Label lblSearching = new Label();
        public Button btnOk = new Button();
        public Button btnCancel = new Button();

        public static UPnPService content = new UPnPService();
        public static List<UPnPDevice> servers = new List<UPnPDevice>();
        public UPnPDeviceFinder discovery = new UPnPDeviceFinder();
        UPnPFinderCallback call = new UPnPFinderCallback();

        /// <summary>
        /// Form Initialisation
        /// </summary>
        public void Initialise()
        {
            tvwServerList.Nodes.Clear();
            frmMainWindow.Size = new Size(300, 360);
            frmMainWindow.Text = "UPnP Server Browser";

            tvwServerList.Location = new Point(20, 12);
            tvwServerList.Size = new Size(260, 268);
            tvwServerList.Enabled = false;

            btnOk.Location = new Point(115, 285);
            btnOk.Text = "Ok";
            btnOk.Click += new EventHandler(btnOk_Click);
            btnCancel.Location = new Point(200, 285);
            btnCancel.Text = "Cancel";
            btnCancel.Click += new EventHandler(btnCancel_Click);

            frmMainWindow.Controls.Add(tvwServerList);
            frmMainWindow.Controls.Add(btnOk);
            frmMainWindow.Controls.Add(btnCancel);
            discovery.StartAsyncFind(discovery.CreateAsyncFind("urn:schemas-upnp-org:device:MediaServer:1", 0, call));
        }

        /// <summary>
        /// Open the form
        /// </summary>
        /// <param name="ifldr"></param>
        /// <returns></returns>
        public DialogResult ShowDialog(ImportFolder ifldr)
        {
            Initialise();
            try
            {
                importFldr = ifldr;
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex);
            }
            frmMainWindow.ShowDialog();
            return frmMainWindow.DialogResult;
        }

        void btnCancel_Click(object sender, EventArgs e)
        {
            frmMainWindow.DialogResult = DialogResult.Cancel;
            frmMainWindow.Close();
        }

        void btnOk_Click(object sender, EventArgs e)
        {
            try {
                Contract_ImportFolder contract = new Contract_ImportFolder();
                if (importFldr.ImportFolderID == 0)
                    contract.ImportFolderID = null;
                else
                    contract.ImportFolderID = importFldr.ImportFolderID;
                contract.ImportFolderName = tvwServerList.SelectedNode.Text;
                TreeNode topparent = tvwServerList.SelectedNode;
                while (topparent.Parent != null)
                    topparent = topparent.Parent;
                contract.ImportFolderLocation = topparent.Tag + "|" +tvwServerList.SelectedNode.Tag.ToString().Trim();
                contract.ImportFolderType = 1;
                contract.IsDropDestination = 0;
                contract.IsDropSource = 0;
                contract.IsWatched = 0;

                JMMServiceImplementation imp = new JMMServiceImplementation();
                Contract_ImportFolder_SaveResponse response = imp.SaveImportFolder(contract);
                if (!string.IsNullOrEmpty(response.ErrorMessage))
                    System.Windows.MessageBox.Show(response.ErrorMessage, "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);

                ServerInfo.Instance.RefreshImportFolders();

                frmMainWindow.DialogResult = DialogResult.OK;
                frmMainWindow.Close();
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex);
                btnCancel_Click(sender, e);
            }
        }
    }

    /// <summary>
    /// Callback class to allow population of tree view
    /// </summary>
    partial class UPnPFinderCallback : IUPnPDeviceFinderCallback
    {
        /// <summary>
        /// when device is found add to tree view
        /// </summary>
        /// <param name="lFindData"></param>
        /// <param name="pDevice"></param>
        public void DeviceAdded(int lFindData, UPnPDevice pDevice)
        {
            var enumerate = pDevice.Services.GetEnumerator();
            TreeNode parent = new TreeNode();
            parent.Text = pDevice.FriendlyName;
            parent.Tag = pDevice.UniqueDeviceName;
            UPnPServerBrowserDialog.tvwServerList.Nodes.Add(parent);

            try
            {
                while (enumerate.MoveNext())
                {
                    try
                    {
                        UPnPService current = enumerate.Current as UPnPService;
                        XDocument content = UPnPData.Browser(current, "0");
                        object[,] structure = UPnPData.buildStructure(current, content);
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
        /// When deviced removed, remove from tree view
        /// </summary>
        /// <param name="lFindData"></param>
        /// <param name="bstrUDN"></param>
        public void DeviceRemoved(int lFindData, string bstrUDN)
        {
            TreeNode node = new TreeNode();
            foreach (UPnPDevice d in UPnPServerBrowserDialog.servers)
            {
                node.Name = d.FriendlyName;
                if (d.UniqueDeviceName == bstrUDN)
                {
                    UPnPServerBrowserDialog.tvwServerList.Nodes.Remove(node);
                }
            }
        }

        /// <summary>
        /// When finished searching give user feedback
        /// </summary>
        /// <param name="lFindData"></param>
        public void SearchComplete(int lFindData)
        {
            UPnPServerBrowserDialog.tvwServerList.Enabled = true;
        }
    }

}
