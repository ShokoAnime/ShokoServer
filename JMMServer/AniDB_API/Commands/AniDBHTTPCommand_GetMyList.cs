using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using JMMServer;

namespace AniDBAPI.Commands
{
    public class AniDBHTTPCommand_GetMyList : AniDBHTTPCommand, IAniDBHTTPCommand
    {
        private List<Raw_AniDB_MyListFile> myListItems = new List<Raw_AniDB_MyListFile>();

        public List<Raw_AniDB_MyListFile> MyListItems
        {
            get { return myListItems; }
            set { myListItems = value; }
        }

        private string username = "";

        public string Username
        {
            get { return username; }
            set { username = value; }
        }

        private string password = "";

        public string Password
        {
            get { return password; }
            set { password = value; }
        }

        private string xmlResult = "";

        public string XmlResult
        {
            get { return xmlResult; }
            set { xmlResult = value; }
        }

        public string GetKey()
        {
            return "AniDBHTTPCommand_GetMyList";
        }

        public virtual enHelperActivityType GetStartEventType()
        {
            return enHelperActivityType.GettingMyListHTTP;
        }

        private void WriteAnimeMyListToFile(string xml)
        {
            string appPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string filePath = Path.Combine(appPath, "MyList");

            if (!Directory.Exists(filePath))
                Directory.CreateDirectory(filePath);

            //string fileName = string.Format("MyList_{0}_{1}.xml", DateTime.Now.ToString("yyyyMMdd"), DateTime.Now.ToString("HHmmss"));
            string fileName = string.Format("MyList.xml");
            string fileNameWithPath = Path.Combine(filePath, fileName);

            StreamWriter sw;
            sw = File.CreateText(fileNameWithPath);
            sw.Write(xml);
            sw.Close();
        }

        private XmlDocument LoadAnimeMyListFromFile()
        {
            string appPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string filePath = Path.Combine(appPath, "MyList");

            if (!Directory.Exists(filePath))
                Directory.CreateDirectory(filePath);

            string fileName = string.Format("MyList.xml");
            string fileNameWithPath = Path.Combine(filePath, fileName);

            XmlDocument docAnime = null;
            if (File.Exists(fileNameWithPath))
            {
                StreamReader re = File.OpenText(fileNameWithPath);
                string rawXML = re.ReadToEnd();
                re.Close();

                docAnime = new XmlDocument();
                docAnime.LoadXml(rawXML);
            }

            return docAnime;
        }

        public virtual enHelperActivityType Process()
        {
            JMMService.LastAniDBMessage = DateTime.Now;
            JMMService.LastAniDBHTTPMessage = DateTime.Now;

            XmlDocument docAnime = AniDBHTTPHelper.GetMyListXMLFromAPI(username, password, ref xmlResult);
            //XmlDocument docAnime = LoadAnimeMyListFromFile();
            //APIUtils.WriteToLog("AniDBHTTPCommand_GetFullAnime: " + xmlResult);

            if (xmlResult.Trim().Length > 0)
                WriteAnimeMyListToFile(xmlResult);

            if (CheckForBan(xmlResult)) return enHelperActivityType.NoSuchAnime;

            if (docAnime != null)
            {
                myListItems = AniDBHTTPHelper.ProcessMyList(docAnime);
                return enHelperActivityType.GotMyListHTTP;
            }
            else
            {
                return enHelperActivityType.NoSuchAnime;
            }
        }

        public AniDBHTTPCommand_GetMyList()
        {
            commandType = enAniDBCommandType.GetMyListHTTP;
        }

        public void Init(string uname, string pword)
        {
            this.username = uname;
            this.password = pword;
            commandID = "MYLIST";
        }
    }
}