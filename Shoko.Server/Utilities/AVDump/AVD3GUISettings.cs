using System.Collections.Generic;
using AVDump3Lib.Settings.Core;
using AVDump3Lib.UI;

namespace Shoko.Server.Utilities.AVDump;


public class AVD3GUISettings : AVD3UISettings {
    public AVD3GUISettings(ISettingStore store) : base(store) {
	}

	public static new IEnumerable<ISettingProperty> GetProperties() {
		return AVD3UISettings.GetProperties();
	}
}
