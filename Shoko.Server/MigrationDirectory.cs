using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Shoko.Server
{
    public class MigrationDirectory
    {
        public string From { get; set; }
        public string To { get; set; }

        public bool ShouldMigrate => (!Directory.Exists(To) && Directory.Exists(From));

        public bool Migrate()
        {
            DirectoryInfo fromDir = new DirectoryInfo(From);
            DirectoryInfo toDir = new DirectoryInfo(To);

            if (!fromDir.Root.Name.Equals(toDir.Root.Name, StringComparison.InvariantCultureIgnoreCase))
            {
                long size = RecursiveDirSize(fromDir);
                DriveInfo info = new DriveInfo(toDir.Root.Name);
                if (info.AvailableFreeSpace < size)
                {
                    MessageBox.Show($"Unable to migrate directory {fromDir} into {toDir} not enough space",
                        "Not enough space", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return false;
                }
            }
            MoveDirectory(From, To);
            return true;
        }

        private long RecursiveDirSize(DirectoryInfo info)
        {
            long size = 0;
            info.GetFiles().ToList().ForEach(a => size += a.Length);
            info.GetDirectories().ToList().ForEach(a => size += RecursiveDirSize(a));
            size += 2048; //MiniSafety
            return size;
        }


        public bool SafeMigrate()
        {
            try
            {
                if (ShouldMigrate)
                {
                    bool result = Migrate();
                    if (result)
                        Utils.GrantAccess(To);
                    return result;
                }
                return true;
            }
            catch
            {
                MessageBox.Show(
                    $"We are unable to move the directory '{From}' to '{To}', please move the directory with explorer",
                    "Migration ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
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