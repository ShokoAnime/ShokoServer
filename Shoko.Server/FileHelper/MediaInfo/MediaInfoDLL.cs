/*  Copyright (c) MediaArea.net SARL. All Rights Reserved.
 *
 *  Use of this source code is governed by a BSD-style license that can
 *  be found in the License.html file in the root of the source tree.
 */

//+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
//
// Microsoft Visual C# wrapper for MediaInfo Library
// See MediaInfo.h for help
//
// To make it working, you must put MediaInfo.Dll
// in the executable folder
//
//+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

using System;
using System.Runtime.InteropServices;
using System.Text;
using Pri.LongPath;
using NLog;

namespace MediaInfoLib
{
    [Flags]
    public enum BufferStatus
    {
        Accepted = 1,
        Filled = 2,
        Updated = 4,
        Finalized = 8
    }

    public enum StreamKind
    {
        General,
        Video,
        Audio,
        Text,
        Other,
        Image,
        Menu
    }

    public enum InfoKind
    {
        Name,
        Text,
        Measure,
        Options,
        NameText,
        MeasureText,
        Info,
        HowTo
    }

    public enum InfoOptions
    {
        ShowInInform,
        Support,
        ShowInSupported,
        TypeOfValue
    }

    public enum InfoFileOptions
    {
        FileOption_Nothing = 0x00,
        FileOption_NoRecursive = 0x01,
        FileOption_CloseAll = 0x02,
        FileOption_Max = 0x04
    };


    public class MediaInfo : IDisposable
    {
        [System.Flags]
        internal enum LoadLibraryFlags : uint
        {
            DONT_RESOLVE_DLL_REFERENCES = 0x00000001,
            LOAD_IGNORE_CODE_AUTHZ_LEVEL = 0x00000010,
            LOAD_LIBRARY_AS_DATAFILE = 0x00000002,
            LOAD_LIBRARY_AS_DATAFILE_EXCLUSIVE = 0x00000040,
            LOAD_LIBRARY_AS_IMAGE_RESOURCE = 0x00000020,
            LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008
        }

        [DllImport("kernel32.dll")]
        internal static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hReservedNull, LoadLibraryFlags dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FreeLibrary(IntPtr hModule);

        private static readonly Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private IntPtr _handle;

        public bool MustUseAnsi { get; set; }
        public Encoding Encoding { get; set; }
        private static System.IntPtr moduleHandle = IntPtr.Zero;

        public MediaInfo()
        {

            if ((_handle == IntPtr.Zero) && !Shoko.Server.Utils.IsRunningOnMono())
            {
                string fullexepath = System.Reflection.Assembly.GetEntryAssembly().Location;
                if (!string.IsNullOrEmpty(fullexepath))
                {
                    FileInfo fi = new FileInfo(fullexepath);
                    fullexepath = Path.Combine(fi.Directory.FullName, Environment.Is64BitProcess ? "x64" : "x86",
                        "MediaInfo.dll");
                    string curlpath = Path.Combine(fi.Directory.FullName, Environment.Is64BitProcess ? "x64" : "x86",
                        "libcurl.dll");

                    Logger.Info(string.Format("Using MediaInfo at: {0}", fullexepath));

                    moduleHandle = LoadLibraryEx(fullexepath, IntPtr.Zero, 0);
                    //curlHandle = LoadLibraryEx(curlpath, IntPtr.Zero, 0);
                }
            }

            try
            {
                _handle = MediaInfo_New();
            }
            catch
            {
                _handle = (IntPtr)0;
            }

            InitializeEncoding();
        }

        ~MediaInfo()
        {
            if (_handle != IntPtr.Zero)
            {
                MediaInfo_Delete(_handle);
            }
            if (moduleHandle != IntPtr.Zero)
            {
                FreeLibrary(moduleHandle);
                moduleHandle = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                MediaInfo_Delete(_handle);
            }
            GC.SuppressFinalize(this);
        }

        private void InitializeEncoding()
        {
            if (Environment.OSVersion.ToString().IndexOf("Windows") != -1)
            {
                // Windows guaranteed UCS-2
                MustUseAnsi = false;
                Encoding = Encoding.Unicode;
            }
            else
            {
                // Linux normally UCS-4. As fallback we try UCS-2 and plain Ansi.
                MustUseAnsi = false;
                Encoding = Encoding.UTF32;

                if (Option("Info_Version", "").StartsWith("MediaInfoLib"))
                {
                    return;
                }

                Encoding = Encoding.Unicode;

                if (Option("Info_Version", "").StartsWith("MediaInfoLib"))
                {
                    return;
                }

                MustUseAnsi = true;
                Encoding = Encoding.Default;

                if (Option("Info_Version", "").StartsWith("MediaInfoLib"))
                {
                    return;
                }

                throw new NotSupportedException("Unsupported MediaInfoLib encoding");
            }
        }

        private IntPtr MakeStringParameter(string value)
        {
            var buffer = Encoding.GetBytes(value);

            Array.Resize(ref buffer, buffer.Length + 4);

            var buf = Marshal.AllocHGlobal(buffer.Length);
            Marshal.Copy(buffer, 0, buf, buffer.Length);

            return buf;
        }

        private string MakeStringResult(IntPtr value)
        {
            if (Encoding == Encoding.Unicode)
            {
                return Marshal.PtrToStringUni(value);
            }
            else if (Encoding == Encoding.UTF32)
            {
                int i = 0;
                for (; i < 1024; i += 4)
                {
                    var data = Marshal.ReadInt32(value, i);
                    if (data == 0)
                    {
                        break;
                    }
                }

                var buffer = new byte[i];
                Marshal.Copy(value, buffer, 0, i);

                return Encoding.GetString(buffer, 0, i);
            }
            else
            {
                return Marshal.PtrToStringAnsi(value);
            }
        }

        public int Open(string fileName)
        {
            var pFileName = MakeStringParameter(fileName);
            try
            {
                if (MustUseAnsi)
                {
                    return (int)MediaInfoA_Open(_handle, pFileName);
                }
                else
                {
                    return (int)MediaInfo_Open(_handle, pFileName);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pFileName);
            }
        }

        public int Open(System.IO.Stream stream)
        {
            if (stream.Length < 1024)
            {
                return 0;
            }

            var isValid = (int)MediaInfo_Open_Buffer_Init(_handle, stream.Length, 0);
            if (isValid == 1)
            {
                var buffer = new byte[16 * 1024];
                long seekStart = 0;
                long totalRead = 0;
                int bufferRead;

                do
                {
                    bufferRead = stream.Read(buffer, 0, buffer.Length);
                    totalRead += bufferRead;

                    var status = (BufferStatus)MediaInfo_Open_Buffer_Continue(_handle, buffer, (IntPtr)bufferRead);

                    if (status.HasFlag(BufferStatus.Finalized) || status <= 0 || bufferRead == 0)
                    {
                        Logger.Trace("Read file offset {0}-{1} ({2} bytes)", seekStart, stream.Position, stream.Position - seekStart);
                        break;
                    }

                    var seekPos = MediaInfo_Open_Buffer_Continue_GoTo_Get(_handle);
                    if (seekPos != -1)
                    {
                        Logger.Trace("Read file offset {0}-{1} ({2} bytes)", seekStart, stream.Position, stream.Position - seekStart);
                        seekPos = stream.Seek(seekPos, System.IO.SeekOrigin.Begin);
                        seekStart = seekPos;
                        MediaInfo_Open_Buffer_Init(_handle, stream.Length, seekPos);
                    }
                } while (bufferRead > 0);

                MediaInfo_Open_Buffer_Finalize(_handle);

                Logger.Trace("Read a total of {0} bytes ({1:0.0}%)", totalRead, totalRead * 100.0 / stream.Length);
            }

            return isValid;
        }

        public void Close()
        {
            MediaInfo_Close(_handle);
        }

        public string Get(StreamKind streamKind, int streamNumber, string parameter, InfoKind infoKind = InfoKind.Text, InfoKind searchKind = InfoKind.Name)
        {
            var pParameter = MakeStringParameter(parameter);
            try
            {
                if (MustUseAnsi)
                {
                    return MakeStringResult(MediaInfoA_Get(_handle, (IntPtr)streamKind, (IntPtr)streamNumber, pParameter, (IntPtr)infoKind, (IntPtr)searchKind));
                }
                else
                {
                    return MakeStringResult(MediaInfo_Get(_handle, (IntPtr)streamKind, (IntPtr)streamNumber, pParameter, (IntPtr)infoKind, (IntPtr)searchKind));
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pParameter);
            }
        }

        public string Get(StreamKind streamKind, int streamNumber, int parameter, InfoKind infoKind)
        {
            if (MustUseAnsi)
            {
                return MakeStringResult(MediaInfoA_GetI(_handle, (IntPtr)streamKind, (IntPtr)streamNumber, (IntPtr)parameter, (IntPtr)infoKind));
            }
            else
            {
                return MakeStringResult(MediaInfo_GetI(_handle, (IntPtr)streamKind, (IntPtr)streamNumber, (IntPtr)parameter, (IntPtr)infoKind));
            }
        }

        public string Option(string option, string value)
        {
            var pOption = MakeStringParameter(option);
            var pValue = MakeStringParameter(value);
            try
            {
                if (MustUseAnsi)
                {
                    return MakeStringResult(MediaInfoA_Option(_handle, pOption, pValue));
                }
                else
                {
                    return MakeStringResult(MediaInfo_Option(_handle, pOption, pValue));
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pOption);
                Marshal.FreeHGlobal(pValue);
            }
        }

        public int State_Get()
        {
            return (int)MediaInfo_State_Get(_handle);
        }

        public int Count_Get(StreamKind streamKind, int streamNumber = -1)
        {
            return (int)MediaInfo_Count_Get(_handle, (IntPtr)streamKind, (IntPtr)streamNumber);
        }

        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfo_New();
        [DllImport("MediaInfo.dll")]
        private static extern void MediaInfo_Delete(IntPtr handle);
        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfo_Open(IntPtr handle, IntPtr fileName);
        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfo_Open_Buffer_Init(IntPtr handle, long fileSize, long fileOffset);
        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfo_Open_Buffer_Continue(IntPtr handle, byte[] buffer, IntPtr bufferSize);
        [DllImport("MediaInfo.dll")]
        private static extern long MediaInfo_Open_Buffer_Continue_GoTo_Get(IntPtr handle);
        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfo_Open_Buffer_Finalize(IntPtr handle);
        [DllImport("MediaInfo.dll")]
        private static extern void MediaInfo_Close(IntPtr handle);
        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfo_GetI(IntPtr handle, IntPtr streamKind, IntPtr streamNumber, IntPtr parameter, IntPtr infoKind);
        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfo_Get(IntPtr handle, IntPtr streamKind, IntPtr streamNumber, IntPtr parameter, IntPtr infoKind, IntPtr searchKind);
        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfo_Option(IntPtr handle, IntPtr option, IntPtr value);
        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfo_State_Get(IntPtr handle);
        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfo_Count_Get(IntPtr handle, IntPtr StreamKind, IntPtr streamNumber);

        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfoA_New();
        [DllImport("MediaInfo.dll")]
        private static extern void MediaInfoA_Delete(IntPtr handle);
        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfoA_Open(IntPtr handle, IntPtr fileName);
        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfoA_Open_Buffer_Init(IntPtr handle, long fileSize, long fileOffset);
        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfoA_Open_Buffer_Continue(IntPtr handle, byte[] buffer, IntPtr bufferSize);
        [DllImport("MediaInfo.dll")]
        private static extern long MediaInfoA_Open_Buffer_Continue_GoTo_Get(IntPtr handle);
        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfoA_Open_Buffer_Finalize(IntPtr handle);
        [DllImport("MediaInfo.dll")]
        private static extern void MediaInfoA_Close(IntPtr handle);
        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfoA_GetI(IntPtr handle, IntPtr streamKind, IntPtr streamNumber, IntPtr parameter, IntPtr infoKind);
        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfoA_Get(IntPtr handle, IntPtr streamKind, IntPtr streamNumber, IntPtr parameter, IntPtr infoKind, IntPtr searchKind);
        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfoA_Option(IntPtr handle, IntPtr option, IntPtr value);
        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfoA_State_Get(IntPtr handle);
        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfoA_Count_Get(IntPtr handle, IntPtr StreamKind, IntPtr streamNumber);
    }
}