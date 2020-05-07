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
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

#pragma warning disable 1591 // Disable XML documentation warnings

namespace MediaInfoWrapper
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
    }

    [Flags]
    public enum Status
    {
        None = 0x00,
        Accepted = 0x01,
        Filled = 0x02,
        Updated = 0x04,
        Finalized = 0x08,
    }

    public class MediaInfoDLL : IDisposable
    {

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

        private static IntPtr moduleHandle = IntPtr.Zero;
        private static IntPtr curlHandle = IntPtr.Zero;

        //Import of DLL functions. DO NOT USE until you know what you do (MediaInfo DLL do NOT use CoTaskMemAlloc to allocate memory)
#if _WINDOWS_
        private const string DLL = "MedidInfo.dll";
#elif _LINUX_
        private const string DLL = "libmediainfo.so.0";
#else
        private const string DLL = "libmediainfo.so.0";
#endif

        [DllImport(DLL)]
        private static extern IntPtr MediaInfo_New();
        [DllImport(DLL)]
        private static extern void   MediaInfo_Delete(IntPtr Handle);
        [DllImport(DLL)]
        private static extern IntPtr MediaInfo_Open(IntPtr Handle, IntPtr FileName);
        [DllImport(DLL)]
        private static extern IntPtr MediaInfo_Open_Buffer_Init(IntPtr Handle, Int64 File_Size, Int64 File_Offset);
        [DllImport(DLL)]
        private static extern IntPtr MediaInfo_Open_Buffer_Continue(IntPtr Handle, IntPtr Buffer, IntPtr Buffer_Size);
        [DllImport(DLL)]
        private static extern Int64  MediaInfo_Open_Buffer_Continue_GoTo_Get(IntPtr Handle);
        [DllImport(DLL)]
        private static extern IntPtr MediaInfo_Open_Buffer_Finalize(IntPtr Handle);
        [DllImport(DLL)]
        private static extern void   MediaInfo_Close(IntPtr Handle);
        [DllImport(DLL)]
        private static extern IntPtr MediaInfo_Inform(IntPtr Handle, IntPtr Reserved);
        [DllImport(DLL)]
        private static extern IntPtr MediaInfo_GetI(IntPtr Handle, IntPtr StreamKind, IntPtr StreamNumber, IntPtr Parameter, IntPtr KindOfInfo);
        [DllImport(DLL)]
        private static extern IntPtr MediaInfo_Get(IntPtr Handle, IntPtr StreamKind, IntPtr StreamNumber, IntPtr Parameter, IntPtr KindOfInfo, IntPtr KindOfSearch);
        [DllImport(DLL, CharSet=CharSet.Unicode)]
        private static extern IntPtr MediaInfo_Option(IntPtr Handle, IntPtr Option, IntPtr Value);
        [DllImport(DLL)]
        private static extern IntPtr MediaInfo_State_Get(IntPtr Handle);
        [DllImport(DLL)]
        private static extern IntPtr MediaInfo_Count_Get(IntPtr Handle, IntPtr StreamKind, IntPtr StreamNumber);

        
        //Helpers
        private static string String_From_UTF32_Ptr(IntPtr Ptr)
        {
            int Length = 0;
            while (Marshal.ReadInt32(Ptr, Length)!=0)
                Length+=4;

            byte[] Buffer = new byte[Length];
            Marshal.Copy(Ptr, Buffer, 0, Buffer.Length);
            return new UTF32Encoding(!BitConverter.IsLittleEndian, false, false).GetString(Buffer);
        }

        private static IntPtr UTF32_Ptr_From_String(string Str)
        {
            Encoding Codec = new UTF32Encoding(!BitConverter.IsLittleEndian, false, false);
            int Length = Codec.GetByteCount(Str);
            byte[] Buffer = new byte[Length+4];
            Codec.GetBytes(Str, 0, Str.Length, Buffer, 0);
            IntPtr Ptr = Marshal.AllocHGlobal(Buffer.Length);
            Marshal.Copy(Buffer, 0, Ptr, Buffer.Length);
            return Ptr;
        }
        
        //MediaInfo class
        public MediaInfoDLL()
        {
            #region Shoko

            if ((Handle == IntPtr.Zero) && IsWindows())
            {
                string fullexepath = Assembly.GetEntryAssembly()?.Location;
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
            catch
            {
                throw;
                Handle = (IntPtr) 0;
            }

            if (!IsWindows())
            {
                UnicodeIs32Bits=true;
                Option("setlocale_LC_CTYPE", "");
            }
            else
            {
                UnicodeIs32Bits=false;
            }
        }

        ~MediaInfoDLL()
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

        public int Open(string FileName)
        {
            #region Shoko

            FileName = IsWindows() && FileName.StartsWith(@"\\")
                ? FileName
                : @"\\?\" + FileName; // add long path prefix if not running on linux, and not a unc path.


            #endregion

            if (Handle == (IntPtr) 0)
                return 0;
            if (UnicodeIs32Bits)
            {
                IntPtr FileName_Ptr = UTF32_Ptr_From_String(FileName);
                int ToReturn = (int)MediaInfo_Open(Handle, FileName_Ptr);
                Marshal.FreeHGlobal(FileName_Ptr);
                return ToReturn;
            }
            else
            {
                IntPtr FileName_Ptr = Marshal.StringToHGlobalUni(FileName);
                int ToReturn = (int)MediaInfo_Open(Handle, FileName_Ptr);
                Marshal.FreeHGlobal(FileName_Ptr);
                return ToReturn;
            }
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
            return MediaInfo_Open_Buffer_Continue_GoTo_Get(Handle);
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

        public string Inform()
        {
            if (Handle == (IntPtr) 0)
                return "Unable to load MediaInfo library";
            if (UnicodeIs32Bits)
                return String_From_UTF32_Ptr(MediaInfo_Inform(Handle, (IntPtr)0));
            return Marshal.PtrToStringUni(MediaInfo_Inform(Handle, (IntPtr) 0));
        }

        public string Get(StreamKind StreamKind, int StreamNumber, string Parameter, InfoKind KindOfInfo,
            InfoKind KindOfSearch)
        {
            if (Handle == (IntPtr) 0)
                return "Unable to load MediaInfo library";
            IntPtr Parameter_Ptr;
            string ToReturn;
            if (UnicodeIs32Bits)
            {
                Parameter_Ptr = UTF32_Ptr_From_String(Parameter);
                ToReturn = String_From_UTF32_Ptr(MediaInfo_Get(Handle, (IntPtr)StreamKind, (IntPtr)StreamNumber, Parameter_Ptr, (IntPtr)KindOfInfo, (IntPtr)KindOfSearch));
                Marshal.FreeHGlobal(Parameter_Ptr);
                return ToReturn;
            }

            Parameter_Ptr = Marshal.StringToHGlobalUni(Parameter);
            ToReturn = Marshal.PtrToStringUni(MediaInfo_Get(Handle, (IntPtr)StreamKind, (IntPtr)StreamNumber, Parameter_Ptr, (IntPtr)KindOfInfo, (IntPtr)KindOfSearch));
            Marshal.FreeHGlobal(Parameter_Ptr);
            return ToReturn;
        }

        public string Get(StreamKind StreamKind, int StreamNumber, int Parameter, InfoKind KindOfInfo)
        {
            if (Handle == (IntPtr) 0)
                return "Unable to load MediaInfo library";
            if (UnicodeIs32Bits)
                return String_From_UTF32_Ptr(MediaInfo_GetI(Handle, (IntPtr)StreamKind, (IntPtr)StreamNumber, (IntPtr)Parameter, (IntPtr)KindOfInfo));

            return Marshal.PtrToStringUni(MediaInfo_GetI(Handle, (IntPtr) StreamKind, (IntPtr) StreamNumber,
                (IntPtr) Parameter, (IntPtr) KindOfInfo));
        }

        public string Option(string Option, string Value)
        {
            if (Handle == (IntPtr) 0)
                return "Unable to load MediaInfo library";
            if (UnicodeIs32Bits)
            {
                IntPtr Option_Ptr=UTF32_Ptr_From_String(Option);
                IntPtr Value_Ptr=UTF32_Ptr_From_String(Value);
                String ToReturn=String_From_UTF32_Ptr(MediaInfo_Option(Handle, Option_Ptr, Value_Ptr));
                Marshal.FreeHGlobal(Option_Ptr);
                Marshal.FreeHGlobal(Value_Ptr);
                return ToReturn;
            }
            else
            {
                IntPtr Option_Ptr=Marshal.StringToHGlobalUni(Option);
                IntPtr Value_Ptr=Marshal.StringToHGlobalUni(Value);
                String ToReturn=Marshal.PtrToStringUni(MediaInfo_Option(Handle, Option_Ptr, Value_Ptr));
                Marshal.FreeHGlobal(Option_Ptr);
                Marshal.FreeHGlobal(Value_Ptr);
                return ToReturn;
            }
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
        private bool UnicodeIs32Bits;

        //Default values, if you know how to set default values in C#, say me
        public string Get(StreamKind StreamKind, int StreamNumber, string Parameter, InfoKind KindOfInfo)
        {
            return Get(StreamKind, StreamNumber, Parameter, KindOfInfo, InfoKind.Name);
        }

        public string Get(StreamKind StreamKind, int StreamNumber, string Parameter)
        {
            return Get(StreamKind, StreamNumber, Parameter, InfoKind.Text, InfoKind.Name);
        }

        public string Get(StreamKind StreamKind, int StreamNumber, int Parameter)
        {
            return Get(StreamKind, StreamNumber, Parameter, InfoKind.Text);
        }

        public string Option(string Option_)
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

        public static bool IsWindows() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    }
}