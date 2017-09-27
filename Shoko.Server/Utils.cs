using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;

using System.Threading;
using Shoko.Models.Server;

using NLog;
using NutzCode.CloudFileSystem;
using Shoko.Commons.Utils;
using Shoko.Models.Enums;
using Directory = Pri.LongPath.Directory;
using File = Pri.LongPath.File;
using Path = Pri.LongPath.Path;

namespace Shoko.Server
{
    public static class Utils
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWow64Process(IntPtr hProcess, [MarshalAs(UnmanagedType.Bool)] out bool isWow64);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr GetModuleHandle(string moduleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        static extern IntPtr GetProcAddress(IntPtr hModule, string methodName);

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public static bool GrantAccess(string path)
        {
            if (IsLinux) return true; //TODO: Implement properly, but as linux uses $HOME for the path, we should be fine.

            if (Directory.Exists(path))
            {
                List<string> perms = Misc.RecursiveGetDirectoriesWithoutEveryonePermission(path);
                if (perms.Count > 0)
                {
                    bool result = Misc.RecursiveSetDirectoriesToEveryoneFullControlPermission(perms);
                    if (result)
                    {
                        perms = Misc.RecursiveGetDirectoriesWithoutEveryonePermission(path);
                        if (perms.Count > 0)
                            result = false;
                    }
                    if (!result)
                    {
                        if (!IsAdministrator())
                        {
                            logger.Info("Needed to set '" + path + "' permissions and failed, restarting as admin.");
                            RestartAsAdmin();
                        }
                        else
                        {
                            logger.Error("Unable to set Everyone permissions to '" + path + "' directory, or subdirectories, please chkdsk or set everyone permissions at hand.");
                            return false;
                        }
                    }

                }
            }
            return true;
        }
  
        public static string CalculateSHA1(string text, Encoding enc)
        {
            byte[] buffer = enc.GetBytes(text);
            SHA1CryptoServiceProvider cryptoTransformSHA1 =
                new SHA1CryptoServiceProvider();
            string hash = BitConverter.ToString(cryptoTransformSHA1.ComputeHash(buffer)).Replace("-", string.Empty);

            return hash;
        }

        public static readonly List<string> Paths = new List<string>
        {
            "JMMServerImage",
            "JMMServerBinary",
            "JMMServerMetro",
            "JMMServerMetroImage",
            "JMMServerStreaming",
            string.Empty
        };

        public static void NetSh(this StreamWriter sw, string path, string verb, string port)
        {
            if (verb == "add")
                sw.WriteLine($@"netsh http add urlacl url=http://+:{port}/{path} sddl=D:(A;;GA;;;S-1-1-0)");
            else
                sw.WriteLine($@"netsh http delete urlacl url=http://+:{port}/{path}");
        }

        public static string acls = ":\\s+(http://(\\*|\\+):({0}).*?/)\\s?\r\n";

        public static void CleanUpDefaultPortsInNetSh(this StreamWriter sw, int[] ports)
        {
            Regex acl = new Regex(string.Format(acls, string.Join("|", ports.Select(a => a.ToString()))),
                RegexOptions.Singleline);
            Process proc = new Process
            {
                StartInfo =
                {
                    FileName = "netsh",
                    Arguments = "http show urlacl",
                    Verb = "runas",
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                }
            };
            proc.Start();
            StreamReader sr = proc.StandardOutput;
            string str = sr.ReadToEnd();
            proc.WaitForExit();
            foreach (Match m in acl.Matches(str))
            {
                if (m.Success)
                {
                    sw.WriteLine($@"netsh http delete urlacl url={m.Groups[1].Value}");
                }
            }
        }

        /// <summary>
        /// Setup system with needed network settings for JMMServer operation. Will invoke an escalation prompt to user. If changing port numbers please give new and old port.
        /// Do NOT add nancy hosted URLs to this. Nancy has an issue with ServiceHost stealing the reservations, and will handle its URLs itself.
        /// </summary>
        /// <param name="OldPort">The port JMMServer was set to run on.</param>
        /// <param name="Port">The port JMMServer will be running on.</param>
        /// <param name="FilePort">The port JMMServer will use for files.</param>
        /// <param name="OldFilePort">The port JMMServer was set to use for files.</param>
        public static bool SetNetworkRequirements(string Port, string FilePort, string OldPort, string OldFilePort)
        {
            string BatchFile = Path.Combine(System.IO.Path.GetTempPath(), "NetworkConfig.bat");
            Process proc = new Process
            {
                StartInfo =
                {
                    FileName = "cmd.exe",
                    Arguments = $@"/c {BatchFile}",
                    Verb = "runas",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = true
                }
            };

            try
            {
                StreamWriter BatchFileStream = new StreamWriter(BatchFile);

                //Cleanup previous
                try
                {
                    BatchFileStream.CleanUpDefaultPortsInNetSh(new[] {int.Parse(OldPort), int.Parse(OldFilePort)});
                    BatchFileStream.WriteLine(
                        "netsh advfirewall firewall delete rule name=\"JMM Server - Client Port\"");
                    BatchFileStream.WriteLine("netsh advfirewall firewall delete rule name=\"JMM Server - File Port\"");
                    BatchFileStream.WriteLine(
                        $@"netsh advfirewall firewall add rule name=""JMM Server - Client Port"" dir=in action=allow protocol=TCP localport={
                                Port
                            }");
                    Paths.ForEach(a => BatchFileStream.NetSh(a, "add", Port));
                    BatchFileStream.WriteLine(
                        $@"netsh advfirewall firewall add rule name=""JMM Server - File Port"" dir=in action=allow protocol=TCP localport={
                                FilePort
                            }");
                    BatchFileStream.NetSh("", "add", FilePort);
                }
                finally
                {
                    BatchFileStream.Close();
                }

                proc.Start();
                proc.WaitForExit();
                int exitCode = proc.ExitCode;
                proc.Close();
                File.Delete(BatchFile);
                if (exitCode != 0)
                {
                    logger.Error("Temporary batch process for netsh and firewall settings returned error code: " +
                                 exitCode);
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return false;
            }

            logger.Info("Network requirements set.");

            return true;
        }

        /*
        public static bool SetNetworkRequirements(string Port = null, string FilePort = null, string oldPort = null,
            string oldFilePort = null)
        {
            string BatchFile = Path.Combine(System.IO.Path.GetTempPath(), "NetworkConfig.bat");
            int exitCode = -1;
            Process proc = new Process();

            proc.StartInfo.FileName = "cmd.exe";
            proc.StartInfo.Arguments = string.Format(@"/c {0}", BatchFile);
            proc.StartInfo.Verb = "runas";
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            proc.StartInfo.UseShellExecute = true;

            try
            {
                StreamWriter BatchFileStream = new StreamWriter(BatchFile);

                #region Cleanup Default Ports

                if (ServerSettings.JMMServerPort != 8111.ToString())
                {
                    BatchFileStream.WriteLine(string.Format(
                        @"netsh http delete urlacl url=http://+:{0}/JMMServerImage", 8111));
                    BatchFileStream.WriteLine(string.Format(
                        @"netsh http delete urlacl url=http://+:{0}/JMMServerBinary", 8111));
                    BatchFileStream.WriteLine(string.Format(
                        @"netsh http delete urlacl url=http://+:{0}/JMMServerMetro", 8111));
                    BatchFileStream.WriteLine(
                        string.Format(@"netsh http delete urlacl url=http://+:{0}/JMMServerMetroImage", 8111));
                    BatchFileStream.WriteLine(string.Format(@"netsh http delete urlacl url=http://+:{0}/JMMServerPlex",
                        8111));
                    BatchFileStream.WriteLine(string.Format(@"netsh http delete urlacl url=http://+:{0}/JMMServerKodi",
                        8111));
                    BatchFileStream.WriteLine(string.Format(@"netsh http delete urlacl url=http://+:{0}/JMMServerREST",
                        8111));
                    BatchFileStream.WriteLine(
                        string.Format(@"netsh http delete urlacl url=http://+:{0}/JMMServerStreaming", 8111));
                    BatchFileStream.WriteLine(
                        string.Format(
                            @"netsh advfirewall firewall delete rule name=""JMM Server - Client Port"" protocol=TCP localport={0}",
                            8111));
                }

                if (ServerSettings.JMMServerFilePort != 8112.ToString())
                {
                    BatchFileStream.WriteLine(string.Format(@"netsh http delete urlacl url=http://+:{0}/JMMFilePort",
                        8112));
                    BatchFileStream.WriteLine(
                        string.Format(
                            @"netsh advfirewall firewall delete rule name=""JMM Server - File Port"" protocol=TCP localport={0}",
                            8112));
                }

                #endregion

                if (!string.IsNullOrEmpty(Port))
                {
                    BatchFileStream.WriteLine(
                        string.Format(
                            @"netsh advfirewall firewall add rule name=""JMM Server - Client Port"" dir=in action=allow protocol=TCP localport={0}",
                            Port));
                    BatchFileStream.WriteLine(
                        string.Format(
                            @"netsh advfirewall firewall delete rule name=""JMM Server - Client Port"" protocol=TCP localport={0}",
                            oldPort));

                    BatchFileStream.WriteLine(
                        string.Format(@"netsh http add urlacl url=http://+:{0}/JMMServerImage user=everyone",
                            Port));
                    BatchFileStream.WriteLine(
                        string.Format(@"netsh http add urlacl url=http://+:{0}/JMMServerBinary user=everyone",
                            Port));
                    BatchFileStream.WriteLine(
                        string.Format(@"netsh http add urlacl url=http://+:{0}/JMMServerMetro user=everyone",
                            Port));
                    BatchFileStream.WriteLine(string.Format(@"netsh http add urlacl url=http://+:{0} user=everyone",Port));
                    BatchFileStream.WriteLine(string.Format(
                        @"netsh http add urlacl url=http://+:{0}/JMMServerMetroImage user=everyone", Port));
                    BatchFileStream.WriteLine(
                        string.Format(@"netsh http add urlacl url=http://+:{0}/JMMServerPlex user=everyone", Port));
                    BatchFileStream.WriteLine(
                        string.Format(@"netsh http add urlacl url=http://+:{0}/JMMServerKodi user=everyone", Port));
                    BatchFileStream.WriteLine(
                        string.Format(@"netsh http add urlacl url=http://+:{0}/JMMServerREST user=everyone", Port));
                    BatchFileStream.WriteLine(string.Format(
                        @"netsh http add urlacl url=http://+:{0}/JMMServerStreaming user=everyone", Port));
                }
                if (!string.IsNullOrEmpty(FilePort))
                {
                    BatchFileStream.WriteLine(
                        string.Format(
                            @"netsh advfirewall firewall add rule name=""JMM Server - File Port"" dir=in action=allow protocol=TCP localport={0}",
                            FilePort));
                    BatchFileStream.WriteLine(
                        string.Format(
                            @"netsh advfirewall firewall delete rule name=""JMM Server - File Port"" protocol=TCP localport={0}",
                            oldFilePort));

                    BatchFileStream.WriteLine(
                        string.Format(@"netsh http add urlacl url=http://+:{0}/JMMFilePort user=everyone",
                            FilePort));
                }

                if (!string.IsNullOrEmpty(oldPort))
                {
                    BatchFileStream.WriteLine(string.Format(
                        @"netsh http delete urlacl url=http://+:{0}/JMMServerImage", oldPort));
                    BatchFileStream.WriteLine(string.Format(
                        @"netsh http delete urlacl url=http://+:{0}/JMMServerBinary", oldPort));
                    BatchFileStream.WriteLine(string.Format(
                        @"netsh http delete urlacl url=http://+:{0}/JMMServerMetro", oldPort));
                    BatchFileStream.WriteLine(
                        string.Format(@"netsh http delete urlacl url=http://+:{0}/JMMServerMetroImage", oldPort));
                    BatchFileStream.WriteLine(string.Format(@"netsh http delete urlacl url=http://+:{0}/JMMServerPlex",
                        oldPort));
                    BatchFileStream.WriteLine(string.Format(@"netsh http delete urlacl url=http://+:{0}/JMMServerKodi",
                        oldPort));
                    BatchFileStream.WriteLine(string.Format(@"netsh http delete urlacl url=http://+:{0}/JMMServerREST",
                        oldPort));
                    BatchFileStream.WriteLine(
                        string.Format(@"netsh http delete urlacl url=http://+:{0}/JMMServerStreaming", oldPort));
                }
                if (!string.IsNullOrEmpty(oldFilePort))
                    BatchFileStream.WriteLine(string.Format(@"netsh http delete urlacl url=http://+:{0}/JMMFilePort",
                        oldFilePort));

                BatchFileStream.Close();

                proc.Start();
                proc.WaitForExit();
                exitCode = proc.ExitCode;
                proc.Close();
                File.Delete(BatchFile);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return false;
            }

            logger.Info("Network requirements set.");

            return true;
        }
        */
        // Function to display parent function
        public static string GetParentMethodName()
        {
            StackTrace stackTrace = new StackTrace();
            StackFrame stackFrame = stackTrace.GetFrame(2);
            MethodBase methodBase = stackFrame.GetMethod();
            return methodBase.Name;
        }

        public class ErrorEventArgs : EventArgs
        {
            public string Message { get; internal set; }

            public string Title { get; internal set; }
            
            public bool IsError { get; internal set; }=true;
        }

        public class CancelReasonEventArgs : CancelEventArgs
        {
            public CancelReasonEventArgs(string reason, string formTitle)
            {
                FormTitle = formTitle;
                Reason = reason;
            }

            public string Reason { get; }
            public string FormTitle { get; }
        }
        
        public static event EventHandler<ErrorEventArgs> ErrorMessage;
        public static event EventHandler<CancelReasonEventArgs> YesNoRequired;

        public static event EventHandler OnEvents;

        public delegate void DispatchHandler(Action a);

        public static event DispatchHandler OnDispatch;

        public static void DoEvents()
        {
  
            OnEvents?.Invoke(null,null);
        }

        public static void MainThreadDispatch(Action a)
        {
            if (OnDispatch != null)
                OnDispatch?.Invoke(a);
            else
            {
                a();
            }

        }
        public static bool ShowYesNo(string title, string msg)
        {
            CancelReasonEventArgs args = new CancelReasonEventArgs(msg, title);
            YesNoRequired?.Invoke(null, args);
            return !args.Cancel;
        }
        public static void ShowErrorMessage(Exception ex)
        {
            //MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            ErrorMessage?.Invoke(null, new ErrorEventArgs {Message = ex.Message});
            logger.Error(ex, ex.ToString());
        }

        public static void ShowErrorMessage(string msg)
        {
            //MessageBox.Show(msg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            ErrorMessage?.Invoke(null, new ErrorEventArgs { Message = msg });
            logger.Error(msg);
        }

        public static void ShowErrorMessage(string title, string msg)
        {
            //MessageBox.Show(msg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            ErrorMessage?.Invoke(null, new ErrorEventArgs { Message = msg , Title=title });
            logger.Error(msg);
        }

        public static void ShowMessage(string title, string msg)
        {
            //MessageBox.Show(msg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            ErrorMessage?.Invoke(null, new ErrorEventArgs { Message = msg, Title = title, IsError=false});
            logger.Error(msg);
        }
        
        public static string GetApplicationVersion(Assembly a)
        {
            return a.GetName().Version.ToString();
        }

        public static string GetApplicationExtraVersion(Assembly a)
        {
            AssemblyInformationalVersionAttribute version =
                (AssemblyInformationalVersionAttribute) a.GetCustomAttribute(
                    typeof(AssemblyInformationalVersionAttribute));
            if (version == null)
            {
                return string.Empty;
            }
            return version.InformationalVersion;
        }

        public static string GetApplicationVersion()
        {
            return GetApplicationVersion(Assembly.GetExecutingAssembly());
        }

        public static string GetApplicationExtraVersion()
        {
            return GetApplicationExtraVersion(Assembly.GetExecutingAssembly());
        }

        public static long GetCurrentUTCTime()
        {
            DateTime dt = DateTime.Now.ToUniversalTime();
            TimeSpan sp = dt.Subtract(new DateTime(1970, 1, 1, 0, 0, 0));
            return (long) sp.TotalSeconds;
        }

        private static string[] escapes = {"SOURCE", "TAKEN", "FROM", "HTTP", "ANN", "ANIMENFO", "ANIDB", "ANIMESUKI"};

        public static string ReparseDescription(string description)
        {
            if (string.IsNullOrEmpty(description)) return string.Empty;

            string val = description;
            val = val.Replace("<br />", Environment.NewLine)
                .Replace("<br/>", Environment.NewLine)
                .Replace("<i>", string.Empty)
                .Replace("</i>", string.Empty)
                .Replace("<b>", string.Empty)
                .Replace("</b>", string.Empty)
                .Replace("[i]", string.Empty)
                .Replace("[/i]", string.Empty)
                .Replace("[b]", string.Empty)
                .Replace("[/b]", string.Empty);
            val = val.Replace("<BR />", Environment.NewLine)
                .Replace("<BR/>", Environment.NewLine)
                .Replace("<I>", string.Empty)
                .Replace("</I>", string.Empty)
                .Replace("<B>", string.Empty)
                .Replace("</B>", string.Empty)
                .Replace("[I]", string.Empty)
                .Replace("[/I]", string.Empty)
                .Replace("[B]", string.Empty)
                .Replace("[/B]", string.Empty);

            string vup = val.ToUpper();
            while (vup.Contains("[URL") || vup.Contains("[/URL]"))
            {
                int a = vup.IndexOf("[URL", StringComparison.Ordinal);
                if (a >= 0)
                {
                    int b = vup.IndexOf("]", a + 1, StringComparison.Ordinal);
                    if (b >= 0)
                    {
                        val = val.Substring(0, a) + val.Substring(b + 1);
                        vup = val.ToUpper();
                    }
                }
                a = vup.IndexOf("[/URL]", StringComparison.Ordinal);
                if (a < 0) continue;
                val = val.Substring(0, a) + val.Substring(a + 6);
                vup = val.ToUpper();
            }
            while (vup.Contains("HTTP:"))
            {
                int a = vup.IndexOf("HTTP:", StringComparison.Ordinal);
                if (a < 0) continue;
                int b = vup.IndexOf(" ", a + 1, StringComparison.Ordinal);
                if (b < 0) break;
                if (vup[b + 1] == '[')
                {
                    int c = vup.IndexOf("]", b + 1, StringComparison.Ordinal);
                    val = val.Substring(0, a) + " " + val.Substring(b + 2, c - b - 2) + val.Substring(c + 1);
                }
                else
                {
                    val = val.Substring(0, a) + val.Substring(b);
                }
                vup = val.ToUpper();
            }
            int d = -1;
            do
            {
                if (d + 1 >= vup.Length)
                    break;
                d = vup.IndexOf("[", d + 1, StringComparison.Ordinal);
                if (d == -1) continue;
                int b = vup.IndexOf("]", d + 1, StringComparison.Ordinal);
                if (b == -1) continue;
                string cont = vup.Substring(d, b - d);
                bool dome = escapes.Any(s => cont.Contains(s));
                if (!dome) continue;
                val = val.Substring(0, d) + val.Substring(b + 1);
                vup = val.ToUpper();
            } while (d != -1);
            d = -1;
            do
            {
                if (d + 1 >= vup.Length)
                    break;

                d = vup.IndexOf("(", d + 1, StringComparison.Ordinal);
                if (d == -1) continue;
                int b = vup.IndexOf(")", d + 1, StringComparison.Ordinal);
                if (b == -1) continue;
                string cont = vup.Substring(d, b - d);
                bool dome = escapes.Any(s => cont.Contains(s));
                if (!dome) continue;
                val = val.Substring(0, d) + val.Substring(b + 1);
                vup = val.ToUpper();
            } while (d != -1);

            d = vup.IndexOf("SOURCE:", StringComparison.Ordinal);
            if (d == -1)
                d = vup.IndexOf("SOURCE :", StringComparison.Ordinal);
            if (d > 0) val = val.Substring(0, d);
            return val.Trim();
        }

        public static string FormatSecondsToDisplayTime(int secs)
        {
            TimeSpan t = TimeSpan.FromSeconds(secs);

            if (t.Hours > 0)
                return $"{t.Hours}:{t.Minutes.ToString().PadLeft(2, '0')}:{t.Seconds.ToString().PadLeft(2, '0')}";
                return $"{t.Minutes}:{t.Seconds.ToString().PadLeft(2, '0')}";
        }

        public static string FormatAniDBRating(double rat)
        {
            // the episode ratings from UDP are out of 1000, while the HTTP AP gives it out of 10
            rat /= 100;

            return $"{rat:0.00}";
        }

        public static int? ProcessNullableInt(string sint)
        {
            if (string.IsNullOrEmpty(sint))
                return null;
                return int.Parse(sint);
        }

        public static string RemoveInvalidFolderNameCharacters(string folderName)
        {
            string ret = folderName.Replace(@"*", string.Empty);
            ret = ret.Replace(@"|", string.Empty);
            ret = ret.Replace(@"\", string.Empty);
            ret = ret.Replace(@"/", string.Empty);
            ret = ret.Replace(@":", string.Empty);
            ret = ret.Replace("\"", string.Empty); // double quote
            ret = ret.Replace(@">", string.Empty);
            ret = ret.Replace(@"<", string.Empty);
            ret = ret.Replace(@"?", string.Empty);
            while (ret.EndsWith("."))
                ret = ret.Substring(0, ret.Length - 1);
            return ret.Trim();
        }

        public static string ReplaceInvalidFolderNameCharacters(string folderName)
        {
            string ret = folderName.Replace(@"*", @"★");
            ret = ret.Replace(@"|", @"¦");
            ret = ret.Replace(@"\", @"\");
            ret = ret.Replace(@"/", @"⁄");
            ret = ret.Replace(@":", @"։");
            ret = ret.Replace("\"", "״"); // double quote
            ret = ret.Replace(@">", @"›");
            ret = ret.Replace(@"<", @"‹");
            ret = ret.Replace(@"?", @"﹖");
            ret = ret.Replace(@"...", @"…");
            if (ret.StartsWith(".", StringComparison.Ordinal)) ret = "․" + ret.Substring(1, ret.Length - 1);
            if (ret.EndsWith(".", StringComparison.Ordinal))
                ret = ret.Substring(0, ret.Length - 1) + "․";
            return ret.Trim();
        }

        public static string GetSortName(string name)
        {
            string newName = name;
            if (newName.ToLower().StartsWith("the ", StringComparison.InvariantCultureIgnoreCase))
                newName = newName.Remove(0, 4);
            if (newName.ToLower().StartsWith("a ", StringComparison.InvariantCultureIgnoreCase))
                newName = newName.Remove(0, 2);

            return newName;
        }

        public static string GetOSInfo()
        {
            //Get Operating system information.
            OperatingSystem os = Environment.OSVersion;
            //Get version information about the os.
            Version vs = os.Version;

            //Variable to hold our return value
            string operatingSystem = string.Empty;

            if (os.Platform == PlatformID.Win32Windows)
            {
                //This is a pre-NT version of Windows
                switch (vs.Minor)
                {
                    case 0:
                        operatingSystem = "95";
                        break;
                    case 10:
                        if (vs.Revision.ToString() == "2222A")
                            operatingSystem = "98SE";
                        else
                            operatingSystem = "98";
                        break;
                    case 90:
                        operatingSystem = "Me";
                        break;
                }
            }
            else if (os.Platform == PlatformID.Win32NT)
            {
                switch (vs.Major)
                {
                    case 3:
                        operatingSystem = "NT 3.51";
                        break;
                    case 4:
                        operatingSystem = "NT 4.0";
                        break;
                    case 5:
                        if (vs.Minor == 0)
                            operatingSystem = "2000";
                        else
                            operatingSystem = "XP";
                        break;
                    case 6:
                        switch (vs.Minor)
                        {
                            case 0:
                                operatingSystem = "Vista / 2008 Server";
                                break;
                            case 1:
                                operatingSystem = "7 / 2008 Server R2";
                                break;
                            case 2:
                                operatingSystem = "8 / 2012 Server";
                                break;
                            case 3:
                                operatingSystem = "8.1 / 2012 Server R2";
                                break;
                            default:
                                operatingSystem = "Unknown";
                                break;
                        }
                        break;
                }
            }
            //Make sure we actually got something in our OS check
            //We don't want to just return " Service Pack 2" or " 32-bit"
            //That information is useless without the OS version.
            if (operatingSystem != string.Empty)
            {
                //Got something.  Let's prepend "Windows" and get more info.
                operatingSystem = "Windows " + operatingSystem;
                //See if there's a service pack installed.
                if (os.ServicePack != string.Empty)
                {
                    //Append it to the OS name.  i.e. "Windows XP Service Pack 3"
                    operatingSystem += " " + os.ServicePack;
                }
                //Append the OS architecture.  i.e. "Windows XP Service Pack 3 32-bit"
                operatingSystem += " " + GetOSArchitecture() + "-bit";
            }
            //Return the information we've gathered.
            return operatingSystem;
        }

        public static string GetMd5Hash(string input)
        {
            using (MD5 md5Hash = MD5.Create())
            {
                return GetMd5Hash(md5Hash, input);
            }
        }

        public static string GetMd5Hash(MD5 md5Hash, string input)
        {
            // Convert the input string to a byte array and compute the hash. 
            byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

            // Create a new Stringbuilder to collect the bytes 
            // and create a string.
            StringBuilder sBuilder = new StringBuilder();

            // Loop through each byte of the hashed data  
            // and format each one as a hexadecimal string. 
            for (int i = 0; i < data.Length; i++)
                sBuilder.Append(data[i].ToString("x2"));

            // Return the hexadecimal string. 
            return sBuilder.ToString();
        }

        public static int GetOSArchitecture() => Is64BitOperatingSystem ? 64 : 32;

        public static bool Is64BitProcess => IntPtr.Size == 8;

        public static bool Is64BitOperatingSystem
        {
            get
            {
                // Clearly if this is a 64-bit process we must be on a 64-bit OS.
                if (Is64BitProcess)
                    return true;
                // Ok, so we are a 32-bit process, but is the OS 64-bit?
                // If we are running under Wow64 than the OS is 64-bit.
                return ModuleContainsFunction("kernel32.dll", "IsWow64Process") &&
                       IsWow64Process(GetCurrentProcess(), out bool isWow64) &&
                       isWow64;
            }
        }

        static bool ModuleContainsFunction(string moduleName, string methodName)
        {
            IntPtr hModule = GetModuleHandle(moduleName);
            if (hModule != IntPtr.Zero)
                return GetProcAddress(hModule, methodName) != IntPtr.Zero;
            return false;
        }

        #region PrettyFilesize

        [DllImport("Shlwapi.dll", CharSet = CharSet.Auto)]
        static extern long StrFormatByteSize(long fileSize,
            [MarshalAs(UnmanagedType.LPTStr)] StringBuilder buffer, int bufferSize);

        public static string FormatByteSize(long fileSize)
        {
            if (IsRunningOnMono()) return GetBytesReadable(fileSize);

            StringBuilder sbBuffer = new StringBuilder(20);
            StrFormatByteSize(fileSize, sbBuffer, 20);
            return sbBuffer.ToString();
        }

        // Returns the human-readable file size for an arbitrary, 64-bit file size 
        // The default format is "0.### XB", e.g. "4.2 KB" or "1.434 GB"
        // http://www.somacon.com/p576.php
        public static string GetBytesReadable(long i)
        {
            // Get absolute value
            long absolute_i = (i < 0 ? -i : i);
            // Determine the suffix and readable value
            string suffix;
            double readable;
            if (absolute_i >= 0x1000000000000000) // Exabyte
            {
                suffix = "EB";
                readable = (i >> 50);
            }
            else if (absolute_i >= 0x4000000000000) // Petabyte
            {
                suffix = "PB";
                readable = (i >> 40);
            }
            else if (absolute_i >= 0x10000000000) // Terabyte
            {
                suffix = "TB";
                readable = (i >> 30);
            }
            else if (absolute_i >= 0x40000000) // Gigabyte
            {
                suffix = "GB";
                readable = (i >> 20);
            }
            else if (absolute_i >= 0x100000) // Megabyte
            {
                suffix = "MB";
                readable = (i >> 10);
            }
            else if (absolute_i >= 0x400) // Kilobyte
            {
                suffix = "KB";
                readable = i;
            }
            else
            {
                return i.ToString("0 B"); // Byte
            }
            // Divide by 1024 to get fractional value
            readable = (readable / 1024);
            // Return formatted number with suffix
            return readable.ToString("0.### ") + suffix;
        }

        #endregion

        public static List<string> GetPossibleSubtitleFiles(string fileName)
        {
            List<string> subtileFiles = new List<string>
            {
                Path.Combine(Path.GetDirectoryName(fileName),
                Path.GetFileNameWithoutExtension(fileName) + ".srt"),
                Path.Combine(Path.GetDirectoryName(fileName),
                Path.GetFileNameWithoutExtension(fileName) + ".ass"),
                Path.Combine(Path.GetDirectoryName(fileName),
                Path.GetFileNameWithoutExtension(fileName) + ".ssa"),
                Path.Combine(Path.GetDirectoryName(fileName),
                Path.GetFileNameWithoutExtension(fileName) + ".idx"),
                Path.Combine(Path.GetDirectoryName(fileName),
                Path.GetFileNameWithoutExtension(fileName) + ".sub"),
                Path.Combine(Path.GetDirectoryName(fileName),
                Path.GetFileNameWithoutExtension(fileName) + ".rar")
            };
            return subtileFiles;
        }

        /// <summary>
        /// This method attempts to take a video resolution, and return something that is closer to a standard
        /// </summary>
        /// <param name="res"></param>
        /// <returns></returns>
        public static string GetStandardisedVideoResolution(string res) => FileQualityFilter.GetResolution(res) ?? res;

        public static int GetVideoWidth(string videoResolution)
        {
            int videoWidth = 0;
            if (videoResolution.Trim().Length > 0)
            {
                string[] dimensions = videoResolution.Split('x');
                if (dimensions.Length > 0) int.TryParse(dimensions[0], out videoWidth);
            }
            return videoWidth;
        }

        public static int GetVideoHeight(string videoResolution)
        {
            int videoHeight = 0;
            if (videoResolution.Trim().Length > 0)
            {
                string[] dimensions = videoResolution.Split('x');
                if (dimensions.Length > 1) int.TryParse(dimensions[1], out videoHeight);
            }
            return videoHeight;
        }

        public static int GetScheduledHours(ScheduledUpdateFrequency freq)
        {
            switch (freq)
            {
                case ScheduledUpdateFrequency.Daily:
                    return 24;
                case ScheduledUpdateFrequency.HoursSix:
                    return 6;
                case ScheduledUpdateFrequency.HoursTwelve:
                    return 12;
                case ScheduledUpdateFrequency.WeekOne:
                    return 24 * 7;
                case ScheduledUpdateFrequency.MonthOne:
                    return 24 * 30;
                case ScheduledUpdateFrequency.Never:
                    return int.MaxValue;
            }

            return int.MaxValue;
        }

        public static void GetFilesForImportFolder(IDirectory sDir, ref List<string> fileList)
        {
            try
            {
                if (sDir == null)
                {
                    logger.Error("Filesystem not found");
                    return;
                }
                // get root level files

                FileSystemResult r = sDir.Populate();
                if (r == null || !r.IsOk)
                {
                    logger.Error($"Unable to retrieve folder {sDir.FullName}");
                    return;
                }

                fileList.AddRange(sDir.Files.Select(a => a.FullName));

                // search sub folders
                foreach (IDirectory dir in sDir.Directories)
                    GetFilesForImportFolder(dir, ref fileList);
            }
            catch (Exception excpt)
            {
                Console.WriteLine(excpt.Message);
            }
        }

        public static void ExecuteCommandSync(object command)
        {
            try
            {
                // create the ProcessStartInfo using "cmd" as the program to be run,
                // and "/c " as the parameters.
                // Incidentally, /c tells cmd that we want it to execute the command that follows,
                // and then exit.
                ProcessStartInfo procStartInfo =
                    new ProcessStartInfo("cmd", "/c " + command)
                    {

                        // The following commands are needed to redirect the standard output.
                        // This means that it will be redirected to the Process.StandardOutput StreamReader.
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        // Do not create the black window.
                        CreateNoWindow = true
                    };
                // Now we create a process, assign its ProcessStartInfo and start it
                Process proc = new Process
                {
                    StartInfo = procStartInfo
                };
                proc.Start();
                // Get the output into a string
                string result = proc.StandardOutput.ReadToEnd();
                // Display the command output.
                Console.WriteLine(result);
            }
            catch
            {
                // Log the exception
            }
        }

        public static void ClearAutoUpdateCache()
        {
            // rmdir /s /q "%userprofile%\wc"
            ExecuteCommandSync("rmdir /s /q \"%userprofile%\\wc\"");
        }

        public static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        ///<summary>Returns the end of a text reader.</summary>
        ///<param name="reader">The reader to read from.</param>
        ///<param name="lineCount">The number of lines to return.</param>
        ///<returns>The last lneCount lines from the reader.</returns>
        public static string[] Tail(this TextReader reader, int lineCount)
        {
            var buffer = new List<string>(lineCount);
            string line;
            for (int i = 0; i < lineCount; i++)
            {
                line = reader.ReadLine();
                if (line == null) return buffer.ToArray();
                buffer.Add(line);
            }

            int lastLine =
                lineCount -
                1; //The index of the last line read from the buffer.  Everything > this index was read earlier than everything <= this indes

            while (null != (line = reader.ReadLine()))
            {
                lastLine++;
                if (lastLine == lineCount) lastLine = 0;
                buffer[lastLine] = line;
            }

            if (lastLine == lineCount - 1) return buffer.ToArray();
            var retVal = new string[lineCount];
            buffer.CopyTo(lastLine + 1, retVal, 0, lineCount - lastLine - 1);
            buffer.CopyTo(0, retVal, lineCount - lastLine - 1, lastLine + 1);
            return retVal;
        }

        public static void RestartAsAdmin()
        {
            if (Utils.IsRunningOnMono()) return; //Again, mono cannot handle this.

            string BatchFile = Path.Combine(System.IO.Path.GetTempPath(), "RestartAsAdmin.bat");
            var exeName = Process.GetCurrentProcess().MainModule.FileName;

            Process proc = new Process();

            proc.StartInfo.FileName = "cmd.exe";
            proc.StartInfo.Arguments = $@"/c {BatchFile}";
            proc.StartInfo.Verb = "runas";
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            proc.StartInfo.UseShellExecute = true;

            try
            {
                StreamWriter BatchFileStream = new StreamWriter(BatchFile);

                try
                {
                    // Wait a few seconds to allow shutdown later on, use task kill just in case still running
                    string batchline =
                        $"timeout 5 && taskkill /F /IM {AppDomain.CurrentDomain.FriendlyName} /fi \"memusage gt 2\" && \"{exeName}\"";
                    logger.Log(LogLevel.Info, "RestartAsAdmin batch line: " + batchline);
                    BatchFileStream.WriteLine(batchline);
                }
                finally
                {
                    BatchFileStream.Close();
                }

                proc.Start();
                ServerSettings.DoServerShutdown(new ServerSettings.ReasonedEventArgs());         
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Error, "Error occured during RestartAsAdmin(): " + ex.Message);
            }
        }

        public static bool IsDirectoryWritable(string dirPath, bool throwIfFails = false)
        {
            try
            {
                using (File.Create(Path.Combine(dirPath, Path.GetRandomFileName()), 1, FileOptions.DeleteOnClose))
                {
                    return true;
                }
            }
            catch
            {
                if (throwIfFails)
                    throw;
                    return false;
            }
        }

        public static bool IsLinux
        {
            get
            {
                int p = (int)Environment.OSVersion.Platform;
                return (p == 4) || (p == 6) || (p == 128);
            }
        }

        public static bool IsRunningOnMono() => Type.GetType("Mono.Runtime") != null;
    }
}