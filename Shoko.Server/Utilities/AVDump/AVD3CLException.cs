using System;
using AVDump3Lib;

namespace Shoko.Server.Utilities.AVDump;

public class AVD3CLException : AVD3LibException {
	public AVD3CLException(string message, Exception innerException) : base(message, innerException) { }
	public AVD3CLException(string message) : base(message) { }
}
