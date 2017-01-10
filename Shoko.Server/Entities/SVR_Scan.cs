using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Shoko.Models;
using Shoko.Models.Server;

namespace Shoko.Server.Entities
{
    public class SVR_Scan : Scan
    {
        public ScanStatus ScanStatus => (ScanStatus)Status;

        public string StatusText
        {
            get
            {
                switch (ScanStatus)
                {
                    case ScanStatus.Finish:
                        return "Finished";
                    case ScanStatus.Running:
                        return "Running";
                    default:
                        return "Standby";
                }
            }
        }

        public List<int> ImportFolderList
        {
            get
            {
                return ImportFolders.Split(',').Select(a=>int.Parse(a)).ToList();
            }
        }
        public string TitleText => CreationTIme.ToString(CultureInfo.CurrentUICulture) + " (" + ImportFolders + ")";
    }
}
