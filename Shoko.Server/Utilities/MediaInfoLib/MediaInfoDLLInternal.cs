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
using System.IO;
using System.Runtime.InteropServices;


#pragma warning disable 1591 // Disable XML documentation warnings

namespace Shoko.Server.Utilities.MediaInfoLib
{
    public enum StreamKind
    {
        General,
        Video,
        Audio,
        Text,
        Other,
        Image,
        Menu,
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

    public enum Status
    {
        None = 0x00,
        Accepted = 0x01,
        Filled = 0x02,
        Updated = 0x04,
        Finalized = 0x08,
    }

    public class MediaInfoDLLInternal : IDisposable
    {
        #region SHOKO

        [Flags]
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

        private static System.IntPtr moduleHandle = IntPtr.Zero;
        private static System.IntPtr curlHandle = IntPtr.Zero;

        #endregion

        //Import of DLL functions. DO NOT USE until you know what you do (MediaInfo DLL do NOT use CoTaskMemAlloc to allocate memory)
        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfo_New();

        [DllImport("MediaInfo.dll")]
        private static extern void MediaInfo_Delete(IntPtr Handle);

        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfo_Open(IntPtr Handle, [MarshalAs(UnmanagedType.LPWStr)] string FileName);

        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfoA_Open(IntPtr Handle, IntPtr FileName);

        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfo_Open_Buffer_Init(IntPtr Handle, Int64 File_Size, Int64 File_Offset);

        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfoA_Open(IntPtr Handle, Int64 File_Size, Int64 File_Offset);

        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfo_Open_Buffer_Continue(IntPtr Handle, IntPtr Buffer, IntPtr Buffer_Size);

        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfoA_Open_Buffer_Continue(IntPtr Handle, Int64 File_Size, byte[] Buffer,
            IntPtr Buffer_Size);

        [DllImport("MediaInfo.dll")]
        private static extern Int64 MediaInfo_Open_Buffer_Continue_GoTo_Get(IntPtr Handle);

        [DllImport("MediaInfo.dll")]
        private static extern Int64 MediaInfoA_Open_Buffer_Continue_GoTo_Get(IntPtr Handle);

        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfo_Open_Buffer_Finalize(IntPtr Handle);

        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfoA_Open_Buffer_Finalize(IntPtr Handle);

        [DllImport("MediaInfo.dll")]
        private static extern void MediaInfo_Close(IntPtr Handle);

        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfo_Inform(IntPtr Handle, IntPtr Reserved);

        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfoA_Inform(IntPtr Handle, IntPtr Reserved);

        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfo_GetI(IntPtr Handle, IntPtr StreamKind, IntPtr StreamNumber,
            IntPtr Parameter, IntPtr KindOfInfo);

        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfoA_GetI(IntPtr Handle, IntPtr StreamKind, IntPtr StreamNumber,
            IntPtr Parameter, IntPtr KindOfInfo);

        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfo_Get(IntPtr Handle, IntPtr StreamKind, IntPtr StreamNumber,
            [MarshalAs(UnmanagedType.LPWStr)] string Parameter, IntPtr KindOfInfo, IntPtr KindOfSearch);

        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfoA_Get(IntPtr Handle, IntPtr StreamKind, IntPtr StreamNumber,
            IntPtr Parameter, IntPtr KindOfInfo, IntPtr KindOfSearch);

        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfo_Option(IntPtr Handle, [MarshalAs(UnmanagedType.LPWStr)] string Option,
            [MarshalAs(UnmanagedType.LPWStr)] string Value);

        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfoA_Option(IntPtr Handle, IntPtr Option, IntPtr Value);

        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfo_State_Get(IntPtr Handle);

        [DllImport("MediaInfo.dll")]
        private static extern IntPtr MediaInfo_Count_Get(IntPtr Handle, IntPtr StreamKind, IntPtr StreamNumber);

        //MediaInfo class
        public MediaInfoDLLInternal()
        {
            #region Shoko

            if ((Handle == IntPtr.Zero) && !IsRunningOnMono())
            {
                string fullexepath = System.Reflection.Assembly.GetEntryAssembly()?.Location;
                if (!string.IsNullOrEmpty(fullexepath))
                {
                    FileInfo fi = new FileInfo(fullexepath);
                    fullexepath = Path.Combine(fi.Directory.FullName, Environment.Is64BitProcess ? "x64" : "x86",
                        "MediaInfo.dll");
                    string curlpath = Path.Combine(fi.Directory.FullName, Environment.Is64BitProcess ? "x64" : "x86",
                        "libcurl.dll");

                    moduleHandle = LoadLibraryEx(fullexepath, IntPtr.Zero, 0);
                    curlHandle = LoadLibraryEx(curlpath, IntPtr.Zero, 0);
                    if (moduleHandle == IntPtr.Zero) throw new FileNotFoundException("Unable to load MediaInfo.dll");
                }
            }

            #endregion

            try
            {
                Handle = MediaInfo_New();
            }
            catch (Exception ex)
            {
                Handle = (IntPtr) 0;
            }
            if (Environment.OSVersion.ToString().IndexOf("Windows", StringComparison.OrdinalIgnoreCase) == -1)
                MustUseAnsi = true;
            else
                MustUseAnsi = false;
        }

        ~MediaInfoDLLInternal()
        {
            if (Handle == (IntPtr) 0) return;
            MediaInfo_Delete(Handle);

            #region Shoko

            if (moduleHandle != IntPtr.Zero)
            {
                FreeLibrary(moduleHandle);
                moduleHandle = IntPtr.Zero;
            }
            if (curlHandle != IntPtr.Zero)
            {
                FreeLibrary(curlHandle);
                curlHandle = IntPtr.Zero;
            }

            #endregion
        }

        public int Open(String FileName)
        {
            #region Shoko

            if (!IsLinux)
                FileName = FileName.StartsWith(@"\\")
                    ? FileName
                    : @"\\?\" + FileName; // add long path prefix if not running on linux, and not a unc path.


            #endregion

            if (Handle == (IntPtr) 0)
                return 0;
            if (MustUseAnsi)
            {
                IntPtr FileName_Ptr = Marshal.StringToHGlobalAnsi(FileName);
                int ToReturn = (int) MediaInfoA_Open(Handle, FileName_Ptr);
                Marshal.FreeHGlobal(FileName_Ptr);
                return ToReturn;
            }
            else
                return (int) MediaInfo_Open(Handle, FileName);
        }

        public int Open_Buffer_Init(Int64 File_Size, Int64 File_Offset)
        {
            if (Handle == (IntPtr) 0) return 0;
            return (int) MediaInfo_Open_Buffer_Init(Handle, File_Size, File_Offset);
        }

        public int Open_Buffer_Continue(IntPtr Buffer, IntPtr Buffer_Size)
        {
            if (Handle == (IntPtr) 0) return 0;
            return (int) MediaInfo_Open_Buffer_Continue(Handle, Buffer, Buffer_Size);
        }

        public Int64 Open_Buffer_Continue_GoTo_Get()
        {
            if (Handle == (IntPtr) 0) return 0;
            return (Int64) MediaInfo_Open_Buffer_Continue_GoTo_Get(Handle);
        }

        public int Open_Buffer_Finalize()
        {
            if (Handle == (IntPtr) 0) return 0;
            return (int) MediaInfo_Open_Buffer_Finalize(Handle);
        }

        public void Close()
        {
            if (Handle == (IntPtr) 0) return;
            MediaInfo_Close(Handle);
        }

        public String Inform()
        {
            if (Handle == (IntPtr) 0)
                return "Unable to load MediaInfo library";
            if (MustUseAnsi)
                return Marshal.PtrToStringAnsi(MediaInfoA_Inform(Handle, (IntPtr) 0));
            else
                return Marshal.PtrToStringUni(MediaInfo_Inform(Handle, (IntPtr) 0));
        }

        public String Get(StreamKind StreamKind, int StreamNumber, String Parameter, InfoKind KindOfInfo,
            InfoKind KindOfSearch)
        {
            if (Handle == (IntPtr) 0)
                return "Unable to load MediaInfo library";
            if (MustUseAnsi)
            {
                IntPtr Parameter_Ptr = Marshal.StringToHGlobalAnsi(Parameter);
                String ToReturn = Marshal.PtrToStringAnsi(MediaInfoA_Get(Handle, (IntPtr) StreamKind,
                    (IntPtr) StreamNumber, Parameter_Ptr, (IntPtr) KindOfInfo, (IntPtr) KindOfSearch));
                Marshal.FreeHGlobal(Parameter_Ptr);
                return ToReturn;
            }
            else
                return Marshal.PtrToStringUni(MediaInfo_Get(Handle, (IntPtr) StreamKind, (IntPtr) StreamNumber,
                    Parameter, (IntPtr) KindOfInfo, (IntPtr) KindOfSearch));
        }

        public String Get(StreamKind StreamKind, int StreamNumber, int Parameter, InfoKind KindOfInfo)
        {
            if (Handle == (IntPtr) 0)
                return "Unable to load MediaInfo library";
            if (MustUseAnsi)
                return Marshal.PtrToStringAnsi(MediaInfoA_GetI(Handle, (IntPtr) StreamKind, (IntPtr) StreamNumber,
                    (IntPtr) Parameter, (IntPtr) KindOfInfo));
            else
                return Marshal.PtrToStringUni(MediaInfo_GetI(Handle, (IntPtr) StreamKind, (IntPtr) StreamNumber,
                    (IntPtr) Parameter, (IntPtr) KindOfInfo));
        }

        public String Option(String Option, String Value)
        {
            if (Handle == (IntPtr) 0)
                return "Unable to load MediaInfo library";
            if (MustUseAnsi)
            {
                IntPtr Option_Ptr = Marshal.StringToHGlobalAnsi(Option);
                IntPtr Value_Ptr = Marshal.StringToHGlobalAnsi(Value);
                String ToReturn = Marshal.PtrToStringAnsi(MediaInfoA_Option(Handle, Option_Ptr, Value_Ptr));
                Marshal.FreeHGlobal(Option_Ptr);
                Marshal.FreeHGlobal(Value_Ptr);
                return ToReturn;
            }
            else
                return Marshal.PtrToStringUni(MediaInfo_Option(Handle, Option, Value));
        }

        public int State_Get()
        {
            if (Handle == (IntPtr) 0) return 0;
            return (int) MediaInfo_State_Get(Handle);
        }

        public int Count_Get(StreamKind StreamKind, int StreamNumber)
        {
            if (Handle == (IntPtr) 0) return 0;
            return (int) MediaInfo_Count_Get(Handle, (IntPtr) StreamKind, (IntPtr) StreamNumber);
        }

        private IntPtr Handle;
        private bool MustUseAnsi;

        //Default values, if you know how to set default values in C#, say me
        public String Get(StreamKind StreamKind, int StreamNumber, String Parameter, InfoKind KindOfInfo)
        {
            return Get(StreamKind, StreamNumber, Parameter, KindOfInfo, InfoKind.Name);
        }

        public String Get(StreamKind StreamKind, int StreamNumber, String Parameter)
        {
            return Get(StreamKind, StreamNumber, Parameter, InfoKind.Text, InfoKind.Name);
        }

        public String Get(StreamKind StreamKind, int StreamNumber, int Parameter)
        {
            return Get(StreamKind, StreamNumber, Parameter, InfoKind.Text);
        }

        public String Option(String Option_)
        {
            return Option(Option_, "");
        }

        public int Count_Get(StreamKind StreamKind)
        {
            return Count_Get(StreamKind, -1);
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                MediaInfo_Delete(Handle);
            }
            GC.SuppressFinalize(this);
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