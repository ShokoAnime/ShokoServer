using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using JMMServer;

namespace AniDBAPI.Commands
{
    public class AniDBHTTPCommand_GetMyList : AniDBHTTPCommand, IAniDBHTTPCommand
    {
        private string xmlResult = "";

        public AniDBHTTPCommand_GetMyList()
        {
            commandType = enAniDBCommandType.GetMyListHTTP;
        }

        public List<Raw_AniDB_MyListFile> MyListItems { get; set; } = new List<Raw_AniDB_MyListFile>();

        public string Username { get; set; } = "";

        public string Password { get; set; } = "";

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

        public virtual enHelperActivityType Process()
        {
            JMMService.LastAniDBMessage = DateTime.Now;
            JMMService.LastAniDBHTTPMessage = DateTime.Now;

            var docAnime = AniDBHTTPHelper.GetMyListXMLFromAPI(Username, Password, ref xmlResult);
            //XmlDocument docAnime = LoadAnimeMyListFromFile();
            //APIUtils.WriteToLog("AniDBHTTPCommand_GetFullAnime: " + xmlResult);

            if (xmlResult.Trim().Length > 0)
                WriteAnimeMyListToFile(xmlResult);

            if (CheckForBan(xmlResult)) return enHelperActivityType.NoSuchAnime;

            if (docAnime != null)
            {
                MyListItems = AniDBHTTPHelper.ProcessMyList(docAnime);
                return enHelperActivityType.GotMyListHTTP;
            }
            return enHelperActivityType.NoSuchAnime;
        }

        private void WriteAnimeMyListToFile(string xml)
        {
            var appPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var filePath = Path.Combine(appPath, "MyList");

            if (!Directory.Exists(filePath))
                Directory.CreateDirectory(filePath);

            //string fileName = string.Format("MyList_{0}_{1}.xml", DateTime.Now.ToString("yyyyMMdd"), DateTime.Now.ToString("HHmmss"));
            var fileName = "MyList.xml";
            var fileNameWithPath = Path.Combine(filePath, fileName);

            StreamWriter sw;
            sw = File.CreateText(fileNameWithPath);
            sw.Write(xml);
            sw.Close();
        }

        private XmlDocument LoadAnimeMyListFromFile()
        {
            var appPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var filePath = Path.Combine(appPath, "MyList");

            if (!Directory.Exists(filePath))
                Directory.CreateDirectory(filePath);

            var fileName = "MyList.xml";
            var fileNameWithPath = Path.Combine(filePath, fileName);

            XmlDocument docAnime = null;
            if (File.Exists(fileNameWithPath))
            {
                var re = File.OpenText(fileNameWithPath);
                var rawXML = re.ReadToEnd();
                re.Close();

                docAnime = new XmlDocument();
                docAnime.LoadXml(rawXML);
            }

            return docAnime;
        }

        public void Init(string uname, string pword)
        {
            Username = uname;
            Password = pword;
            commandID = "MYLIST";
        }
    }
}