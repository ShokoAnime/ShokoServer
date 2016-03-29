using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;

namespace JMMServer
{
	public class DataAccessHelper
	{
		public static void GetShareAndPath(string fileName, List<ImportFolder> shares, ref int importFolderID, ref string filePath)
		{
			// TODO make sure that import folders are not sub folders of each other
			// TODO make sure import folders do not contain a trailing "\"
			importFolderID = -1;
			foreach (ImportFolder ifolder in shares)
			{
				string importLocation = ifolder.ImportFolderLocation;
				string importLocationFull = importLocation.TrimEnd('\\');

                // add back the trailing back slashes
                importLocationFull = importLocationFull + "\\";

                importLocation = importLocation.TrimEnd('\\');
                if (fileName.StartsWith(importLocationFull))
				{
					importFolderID = ifolder.ImportFolderID;
					filePath = fileName.Replace(importLocation, "");
					filePath = filePath.TrimStart('\\');
					break;
				}
                else if(fileName.Contains("http"))
                {
                    string location = string.Format("{0}|{1}",fileName.Split('|')[0], fileName.Split('|')[1]);
                    if (ifolder.ImportFolderLocation == location) {
                        importFolderID = ifolder.ImportFolderID;
                        filePath = string.Format("{0}|{1}", fileName.Split('|')[2], fileName.Split('|')[3]);
                        filePath = filePath.TrimStart('\\');
                        break;
                    }
                }
			}
		}
	}
}
