﻿using AVDump3Lib.Information;
using AVDump3Lib.Misc;
using AVDump3Lib.Modules;
using AVDump3Lib.Processing;
using AVDump3Lib.Reporting;
using AVDump3Lib.Settings;
using AVDump3Lib.Settings.CLArguments;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace AVDump3CL;

//public class AVD3CLInstance {
//	static private void Start(string[] args) {
//		if(ProcessFromFileArgument(ref args)) return;

//		var serviceCollection = new ServiceCollection();

//		serviceCollection.AddSingleton<IAVD3InformationModule, AVD3InformationModule>();
//		serviceCollection.AddSingleton<IAVD3ProcessingModule, AVD3ProcessingModule>();
//		serviceCollection.AddSingleton<IAVD3ReportingModule, AVD3ReportingModule>();
//		serviceCollection.AddSingleton<IAVD3SettingsModule, AVD3SettingsModule>();
//		serviceCollection.AddSingleton<IAVD3CLModule, AVD3CLModule>();


//		var serviceProvider = serviceCollection.BuildServiceProvider();


//		var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

//		using var scope = scopeFactory.CreateScope();
//		var settingsHandler = scope.ServiceProvider.GetRequiredService<ISettingsHandler>();
//	}

//	private static bool ProcessFromFileArgument(ref string[] args) {
//		if(args.Length > 0 && args[0].Equals("FROMFILE")) {
//			if(args.Length < 2 || !File.Exists(args[1])) {
//				Console.WriteLine("FROMFILE: File not found");
//				return false;
//			}
//			args = File.ReadLines(args[1]).Where(x => !x.StartsWith("//") && !string.IsNullOrWhiteSpace(x)).Select(x => x.Replace("\r", "")).Concat(args.Skip(2)).ToArray();

//		}
//		return true;
//	}
//}

class Program {
	[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
	private static extern IntPtr GetCommandLine();

	static void Main(string[] args) {
		if(args.Contains("PARSEARGS") && Utils.UsingWindows) {
			var ptr = GetCommandLine();
			var commandLine = " " + (Marshal.PtrToStringAuto(ptr) ?? "");

			if((commandLine.Count(x => x == '"') % 2) != 0) {
				Console.WriteLine("When PARSEARGS is enabled, double quote count needs to be even!");
				if(Utils.UsingWindows) Console.Read();
				return;
			}

			args = Regex.Matches(commandLine, @"""(?:""""|[^""])*(?:"" |""$)|'(?:''|[^'])*(?:' |'$)|[^"" ]+(?: |$)")
				.OfType<Match>()
				.Select(x => (commandLine[x.Index - 1] switch { '\'' => x.Value.Replace("''", "'").Trim(), '"' => x.Value.Replace("\"\"", "\""), _ => x.Value }).Trim())
				.Skip(1).ToArray();
		}

		if(args.Contains("UTF8OUT")) {
			Console.OutputEncoding = Encoding.UTF8;
		}

		var moduleManagement = CreateModules();
		moduleManagement.RaiseIntialize();

		var settingsModule = moduleManagement.GetModule<AVD3SettingsModule>();

		string[] pathsToProcess;
		try {
			var parseResult = CLSettingsHandler.ParseArgs(settingsModule.SettingProperties, args);
			if(args.Contains("PRINTARGS")) {
				foreach(var arg in parseResult.RawArgs) Console.WriteLine(arg);
				Console.WriteLine();
			}

			if(!parseResult.Success) {
				Console.WriteLine(parseResult.Message);
				if(Utils.UsingWindows) Console.Read();
				return;
			}

			if(parseResult.PrintHelp) {
				CLSettingsHandler.PrintHelp(settingsModule.SettingProperties, parseResult.PrintHelpTopic, args.Length != 0);
				return;
			}


			var settingsStore = settingsModule.BuildStore();
			foreach(var settingValue in parseResult.SettingValues) {
				settingsStore.SetPropertyValue(settingValue.Key, settingValue.Value);
			}

			pathsToProcess = parseResult.UnnamedArgs.ToArray();

		} catch(Exception ex) {
			Console.WriteLine("Error while parsing commandline arguments:");
			Console.WriteLine(ex.Message);
			return;
		}


		var moduleInitResult = moduleManagement.RaiseInitialized();
		if(moduleInitResult.CancelStartup) {
			if(!string.IsNullOrEmpty(moduleInitResult.Reason)) {
				Console.WriteLine("Startup Cancel: " + moduleInitResult.Reason);
			}
			return;
		}


		var clModule = moduleManagement.GetModule<AVD3CLModule>();
		clModule.Process(pathsToProcess.ToArray());


		moduleManagement.Shutdown();
	}

	private static AVD3ModuleManagement CreateModules() {
		var moduleManagement = new AVD3ModuleManagement();
		moduleManagement.LoadModuleFromType(typeof(AVD3CLModule));
		moduleManagement.LoadModules(AppDomain.CurrentDomain.BaseDirectory ?? throw new Exception("AppDomain.CurrentDomain.BaseDirectory is null"));
		moduleManagement.LoadModuleFromType(typeof(AVD3InformationModule));
		moduleManagement.LoadModuleFromType(typeof(AVD3ProcessingModule));
		moduleManagement.LoadModuleFromType(typeof(AVD3ReportingModule));
		moduleManagement.LoadModuleFromType(typeof(AVD3SettingsModule));
		return moduleManagement;
	}
}
