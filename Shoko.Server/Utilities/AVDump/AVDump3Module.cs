using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Threading;
using AVDump3Lib;
using AVDump3Lib.Information;
using AVDump3Lib.Modules;
using AVDump3Lib.Processing;
using AVDump3Lib.Processing.StreamConsumer;
using AVDump3Lib.Reporting;
using AVDump3Lib.Settings;
using AVDump3Lib.Settings.Core;
using AVDump3Lib.UI;

namespace Shoko.Server.Utilities.AVDump;

public class AVDump3Module : AVD3UIModule, IAVDump3ProgressProvider {
    public override AVD3Console Console { get; } = new();
    protected override AVD3UISettings Settings => _settings;
    private AVD3GUISettings _settings = null!;
    private ISettingStore SettingStore { get; set; }
    private StringBuilder _output = new();
    public string Output => _output.ToString();
    private readonly BytesReadProgress _bytesReadProgress = new ();

    public override IBytesReadProgress CreateBytesReadProgress() => _bytesReadProgress;
    public bool Finished { get; set; } = true;

	public override void Initialize(IReadOnlyCollection<IAVD3Module> modules) {
        base.Initialize(modules);
        InitializeSettings(new SettingsModuleInitResult(SettingStore));
    }

    protected override void OnProcessingStarting(CancellationTokenSource cts)
    {
        Finished = false;
        base.OnProcessingStarting(cts);
        try
        {
            Console.ConsoleWrite -= OnConsoleWrite;
        }
        catch
        {
            // ignore
        }

        _output = new StringBuilder();
        Console.ConsoleWrite += OnConsoleWrite;
    }
    
    private void OnConsoleWrite(string s)
    {
        lock (_output) _output.Append(s);
    }

    protected override void ProcessException(Exception ex) {
	}

    public static AVD3ModuleManagement Create() {
        var moduleManagement = CreateModules();
        var module = moduleManagement.GetModule<AVDump3Module>();
        module.SettingStore = new SettingStore(AVD3GUISettings.GetProperties().ToImmutableArray());
        foreach (var property in module.SettingStore.SettingProperties)
        {
            switch (property.Name)
            {
                case "Consumers" when property.Group.Name == "Processing":
                    module.SettingStore.SetPropertyValue(property,
                        new[]
                        {
                            new ConsumerSettings("ED2K", Array.Empty<string>()),
                            new ConsumerSettings("MD5", Array.Empty<string>()),
                            new ConsumerSettings("CRC32", Array.Empty<string>()),
                            new ConsumerSettings("SHA1", Array.Empty<string>())
                        }.ToImmutableArray());
                    break;
                case "Reports" when property.Group.Name == "Reporting":
                    module.SettingStore.SetPropertyValue(property, new[] {"AVD3"}.ToImmutableArray());
                    break;
                case "PrintReports" when property.Group.Name == "Reporting":
                    module.SettingStore.SetPropertyValue(property, true);
                    break;
            }
        }
        module._settings = new AVD3GUISettings(module.SettingStore);
        moduleManagement.RaiseIntialize();
		return moduleManagement;
	}
	public static bool Run(AVD3ModuleManagement moduleManagement) {
        var moduleInitResult = moduleManagement.RaiseInitialized();
		if(moduleInitResult.CancelStartup) {
			if(!string.IsNullOrEmpty(moduleInitResult.Reason)) {
				System.Console.WriteLine("Startup Cancel: " + moduleInitResult.Reason);
			}
			return false;
		}
		return true;
	}

	private static AVD3ModuleManagement CreateModules() {
		var moduleManagement = new AVD3ModuleManagement();
		moduleManagement.LoadModuleFromType(typeof(AVDump3Module));
		moduleManagement.LoadModuleFromType(typeof(AVD3InformationModule));
		moduleManagement.LoadModuleFromType(typeof(AVD3ProcessingModule));
		moduleManagement.LoadModuleFromType(typeof(AVD3ReportingModule));
        moduleManagement.LoadModuleFromType(typeof(AVD3SettingsModule));
		return moduleManagement;
	}

    public AVDump3Module()
    {
        try
        {
            AVDump3Lib.Misc.Utils.AddNativeLibraryResolver();
        }
        catch
        {
            // ignore
        }
    }

	protected override void OnException(AVD3LibException ex)
    {
        _output.AppendLine(ex.ToString());
    }

    protected override void OnProcessingFinished()
    {
        base.OnProcessingFinished();
        Finished = true;
    }

    public FileProgress[] GetProgress() {
        return _bytesReadProgress.GetProgress();
    }
}
