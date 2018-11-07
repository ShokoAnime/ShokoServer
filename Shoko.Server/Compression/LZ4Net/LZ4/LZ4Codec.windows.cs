#region license

/*
Copyright (c) 2013-2017, Milosz Krajewski
All rights reserved.

Redistribution and use in source and binary forms, with or without modification, are permitted provided 
that the following conditions are met:

* Redistributions of source code must retain the above copyright notice, this list of conditions 
  and the following disclaimer.

* Redistributions in binary form must reproduce the above copyright notice, this list of conditions 
  and the following disclaimer in the documentation and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED 
WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR 
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE 
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT 
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, 
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN 
IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

#endregion

using System;
using System.Runtime.CompilerServices;
using LZ4.Services;
using Microsoft.Win32;

namespace LZ4
{
    public static partial class LZ4Codec
    {
        /// <summary>Determines whether VS2015 runtime is installed. 
        /// Note, on Mono the Registry class is not available at all, 
        /// so access to it have to be isolated.</summary>
        /// <returns><c>true</c> it VS2010 runtime is installed, <c>false</c> otherwise.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Has2015Runtime()
        {
            var keyName =
                IntPtr.Size == 4 ? @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x86" :
                    IntPtr.Size == 8 ? @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64" :
                        null;
            if (keyName == null)
                return false;

            var key = Registry.LocalMachine.OpenSubKey(keyName, false);
            if (key == null)
                return false;

            var value = key.GetValue(@"Major");
            if (value == null)
                return false;

            return Convert.ToUInt32(value) == 14;
        }

#if UNSAFE
        /// <summary>Initializes codecs from LZ4n.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InitializeLZ4n()
        {
            _service_N32 = TryService<Unsafe32LZ4Service>();
            _service_N64 = TryService<Unsafe64LZ4Service>();
        }
#endif
        /// <summary>Initializes codecs from LZ4s.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InitializeLZ4s()
        {
            _service_S32 = TryService<Safe32LZ4Service>();
            _service_S64 = TryService<Safe64LZ4Service>();
        }

        // ReSharper restore InconsistentNaming
    }
}
