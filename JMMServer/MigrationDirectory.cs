using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace JMMServer
{
    public class MigrationDirectory
    {
        public string From { get; set; }
        public string To { get; set; }

        public bool ShouldMigrate => (!Directory.Exists(To) && Directory.Exists(From));

        public void Migrate()
        {
            MoveDirectory(From,To);           
        }


        public bool SafeMigrate()
        {
            try
            {
                if (ShouldMigrate)
                {
                    Migrate();
                    Utils.GrantAccess(To);
                }
                return true;
            }
            catch (Exception e)
            {
                MessageBox.Show($"We are unable to move the directory '{From}' to '{To}', please move the directory with explorer", "Migration ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }


        private void MoveDirectory(string @from, string to)
        {
            DirectoryInfo fromDir = new DirectoryInfo(@from);
            DirectoryInfo toDir = new DirectoryInfo(to);

            if (fromDir.Root.Name.Equals(toDir.Root.Name, StringComparison.InvariantCultureIgnoreCase))
            {
                Directory.Move(@from, to);
                return;
            }
            Directory.CreateDirectory(to);
            foreach (FileInfo file in fromDir.GetFiles())
            {
                string newPath = Path.Combine(to, file.Name);
                file.CopyTo(newPath);
                file.Delete();
            }
            foreach (DirectoryInfo subDir in fromDir.GetDirectories())
            {
                string newPath = Path.Combine(to, subDir.Name);
                MoveDirectory(subDir.FullName, newPath);
            }
            Directory.Delete(@from, true);
        }
    }
}
