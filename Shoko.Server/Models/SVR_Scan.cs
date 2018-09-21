using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Shoko.Commons.Extensions;
using Shoko.Models.Server;
using Shoko.Server.Repositories;


namespace Shoko.Server.Models
{
    public class SVR_Scan : Scan
    {
        public DateTime CreationTIme;

        public string TitleText
        {
            get
            {
                return CreationTime.ToString(CultureInfo.CurrentUICulture) + " (" + string.Join(" | ",
                           this.GetImportFolderList()
                               .Select(a => Repo.Instance.ImportFolder.GetByID(a))
                               .Where(a => a != null)
                               .Select(a => a.ImportFolderLocation
                                   .Split(
                                       new[]
                                       {
                                           Path.PathSeparator, Path.DirectorySeparatorChar,
                                           Path.AltDirectorySeparatorChar
                                       }, StringSplitOptions.RemoveEmptyEntries)
                                   .LastOrDefault())
                               .ToArray()) + ")";
            }
        }
    }
}