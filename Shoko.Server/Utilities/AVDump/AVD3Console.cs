using System;
using System.Collections.Generic;
using AVDump3Lib.UI;

namespace Shoko.Server.Utilities.AVDump;

public class AVD3Console : IAVD3UIConsole {
	public event Action<string> ConsoleWrite = delegate { };

	private class NopDisposable : IDisposable { public void Dispose() { } }
	public IDisposable LockConsole() { return new NopDisposable(); }
	public void WriteLine(IEnumerable<string> values) {
		lock(this) ConsoleWrite(string.Join("\n", values));
	}
	public void WriteLine(string value) {
		lock(this) ConsoleWrite(value);
	}
}
