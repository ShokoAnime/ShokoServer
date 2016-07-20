using System.Collections.Generic;
using JMMServer.Entities;

namespace JMMServer
{
    public class DataAccessHelper
    {
        public static void GetShareAndPath(string fileName, List<ImportFolder> shares, ref int importFolderID,
            ref string filePath)
        {
            // TODO make sure that import folders are not sub folders of each other
            // TODO make sure import folders do not contain a trailing "\"
            importFolderID = -1;
            foreach (ImportFolder ifolder in shares)
            {
                string importLocation = ifolder.ImportFolderLocation.Replace("\\","/");
                string importLocationFull = importLocation.TrimEnd('/');

                // add back the trailing back slashes
                importLocationFull = importLocationFull + "/";

                importLocation = importLocation.TrimEnd('/');
                if (fileName.StartsWith(importLocationFull))
                {
                    importFolderID = ifolder.ImportFolderID;
                    filePath = fileName.Replace(importLocation, "");
                    filePath = filePath.TrimStart('/');
                    break;
                }
            }
        }
    }
}