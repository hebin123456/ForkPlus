using System;
using System.Collections.Generic;
using System.IO;
using ForkPlus.Settings;

namespace ForkPlus
{
	public class ExternalToolManager
	{
		public static class ToolTypeValue
		{
			public const string Custom = "Custom";

			public const string AraxisMerge = "AraxisMerge";

			public const string BeyondCompare = "BeyondCompare";

			public const string Cursor = "Cursor";

			public const string KDiff3 = "KDiff3";

			public const string P4Merge = "P4Merge";

			public const string VSCode = "VSCode";

			public const string VisualStudio = "VisualStudio";

			public const string Unity3d = "Unity3d";

			public const string WinMerge = "WinMerge";

			public const string Zed = "Zed";
		}

		public static readonly ToolDefinition[] MergeToolDefinitions = new ToolDefinition[9]
		{
			new ToolDefinition(ToolType.AraxisMerge, new string[3] { "%localappdata%\\Apps\\Araxis\\Araxis Merge\\compare.exe", "%ProgramW6432%\\Araxis\\Araxis Merge\\compare.exe", "%programfiles(x86)%\\Araxis\\Araxis Merge\\compare.exe" }, new string[8] { "-wait", "-merge", "-3", "-a1", "\"$BASE\"", "\"$REMOTE\"", "\"$LOCAL\"", "\"$MERGED\"" }),
			new ToolDefinition(ToolType.BeyondCompare, new string[4] { "%ProgramW6432%\\Beyond Compare 5\\BCompare.exe", "%programfiles(x86)%\\Beyond Compare 5\\BCompare.exe", "%ProgramW6432%\\Beyond Compare 4\\BCompare.exe", "%programfiles(x86)%\\Beyond Compare 4\\BCompare.exe" }, new string[4] { "\"$REMOTE\"", "\"$LOCAL\"", "\"$BASE\"", "\"$MERGED\"" }),
			new ToolDefinition(ToolType.Cursor, new string[1] { "%localappdata%\\Programs\\cursor\\Cursor.exe" }, new string[7] { "-n", "--wait", "--merge", "\"$REMOTE\"", "\"$LOCAL\"", "\"$BASE\"", "\"$MERGED\"" }),
			new ToolDefinition(ToolType.KDiff3, new string[4] { "%ProgramW6432%\\KDiff3\\kdiff3.exe", "%programfiles%\\KDiff3\\kdiff3.exe", "%ProgramW6432%\\KDiff3\\bin\\kdiff3.exe", "%programfiles%\\KDiff3\\bin\\kdiff3.exe" }, new string[6] { "\"$REMOTE\"", "-b", "\"$BASE\"", "\"$LOCAL\"", "-o", "\"$MERGED\"" }),
			new ToolDefinition(ToolType.P4Merge, new string[2] { "%ProgramW6432%\\Perforce\\p4merge.exe", "%programfiles%\\Perforce\\p4merge.exe" }, new string[4] { "\"$BASE\"", "\"$REMOTE\"", "\"$LOCAL\"", "\"$MERGED\"" }),
			new ToolDefinition(ToolType.VSCode, new string[3] { "%localappdata%\\Programs\\Microsoft VS Code\\Code.exe", "%programfiles(x86)%\\Microsoft VS Code\\Code.exe", "%programfiles%\\Microsoft VS Code\\Code.exe" }, new string[7] { "-n", "--wait", "--merge", "\"$REMOTE\"", "\"$LOCAL\"", "\"$BASE\"", "\"$MERGED\"" }),
			new ToolDefinition(ToolType.VisualStudio, new string[3] { "%ProgramW6432%\\Microsoft Visual Studio\\2022\\Community\\Common7\\IDE\\CommonExtensions\\Microsoft\\TeamFoundation\\Team Explorer\\vsDiffMerge.exe", "%programfiles(x86)%\\Microsoft Visual Studio\\2019\\Community\\Common7\\IDE\\CommonExtensions\\Microsoft\\TeamFoundation\\Team Explorer\\vsDiffMerge.exe", "%programfiles(x86)%\\Microsoft Visual Studio\\2017\\Community\\Common7\\IDE\\CommonExtensions\\Microsoft\\TeamFoundation\\Team Explorer\\vsDiffMerge.exe" }, new string[5] { "\"$REMOTE\"", "\"$LOCAL\"", "\"$BASE\"", "\"$MERGED\"", "/m" }),
			new ToolDefinition(ToolType.Unity3d, new string[2] { "%ProgramW6432%\\Unity\\Hub\\Editor\\2019.4.16f1\\Editor\\Data\\Tools\\UnityYAMLMerge.exe", "%programfiles%\\Unity\\Hub\\Editor\\2019.4.16f1\\Editor\\Data\\Tools\\UnityYAMLMerge.exe" }, new string[6] { "merge", "-p", "\"$BASE\"", "\"$REMOTE\"", "\"$LOCAL\"", "\"$MERGED\"" }),
			new ToolDefinition(ToolType.WinMerge, new string[3] { "%localappdata%\\Programs\\WinMerge\\WinMergeU.exe", "%programfiles%\\WinMerge\\WinMergeU.exe", "%programfiles(x86)%\\WinMerge\\WinMergeU.exe" }, new string[7] { "-u", "-e", "\"$REMOTE\"", "\"$BASE\"", "\"$LOCAL\"", "-o", "\"$MERGED\"" })
		};

		public static readonly ToolDefinition[] DiffToolDefinitions = new ToolDefinition[8]
		{
			new ToolDefinition(ToolType.AraxisMerge, new string[3] { "%localappdata%\\Apps\\Araxis\\Araxis Merge\\compare.exe", "%ProgramW6432%\\Araxis\\Araxis Merge\\compare.exe", "%programfiles(x86)%\\Araxis\\Araxis Merge\\compare.exe" }, new string[2] { "\"$REMOTE\"", "\"$LOCAL\"" }),
			new ToolDefinition(ToolType.BeyondCompare, new string[4] { "%ProgramW6432%\\Beyond Compare 5\\BCompare.exe", "%programfiles(x86)%\\Beyond Compare 5\\BCompare.exe", "%ProgramW6432%\\Beyond Compare 4\\BCompare.exe", "%programfiles(x86)%\\Beyond Compare 4\\BCompare.exe" }, new string[2] { "\"$REMOTE\"", "\"$LOCAL\"" }),
			new ToolDefinition(ToolType.Cursor, new string[1] { "%localappdata%\\Programs\\cursor\\Cursor.exe" }, new string[4] { "--diff", "--wait", "\"$REMOTE\"", "\"$LOCAL\"" }),
			new ToolDefinition(ToolType.KDiff3, new string[4] { "%ProgramW6432%\\KDiff3\\kdiff3.exe", "%programfiles%\\KDiff3\\kdiff3.exe", "%ProgramW6432%\\KDiff3\\bin\\kdiff3.exe", "%programfiles%\\KDiff3\\bin\\kdiff3.exe" }, new string[2] { "\"$REMOTE\"", "\"$LOCAL\"" }),
			new ToolDefinition(ToolType.P4Merge, new string[2] { "%ProgramW6432%\\Perforce\\p4merge.exe", "%programfiles%\\Perforce\\p4merge.exe" }, new string[2] { "\"$REMOTE\"", "\"$LOCAL\"" }),
			new ToolDefinition(ToolType.VSCode, new string[3] { "%localappdata%\\Programs\\Microsoft VS Code\\Code.exe", "%programfiles(x86)%\\Microsoft VS Code\\Code.exe", "%programfiles%\\Microsoft VS Code\\Code.exe" }, new string[4] { "--diff", "--wait", "\"$REMOTE\"", "\"$LOCAL\"" }),
			new ToolDefinition(ToolType.VisualStudio, new string[3] { "%ProgramW6432%\\Microsoft Visual Studio\\2022\\Community\\Common7\\IDE\\CommonExtensions\\Microsoft\\TeamFoundation\\Team Explorer\\vsDiffMerge.exe", "%programfiles(x86)%\\Microsoft Visual Studio\\2019\\Community\\Common7\\IDE\\CommonExtensions\\Microsoft\\TeamFoundation\\Team Explorer\\vsDiffMerge.exe", "%programfiles(x86)%\\Microsoft Visual Studio\\2017\\Community\\Common7\\IDE\\CommonExtensions\\Microsoft\\TeamFoundation\\Team Explorer\\vsDiffMerge.exe" }, new string[3] { "\"$REMOTE\"", "\"$LOCAL\"", "/t" }),
			new ToolDefinition(ToolType.WinMerge, new string[3] { "%localappdata%\\Programs\\WinMerge\\WinMergeU.exe", "%programfiles%\\WinMerge\\WinMergeU.exe", "%programfiles(x86)%\\WinMerge\\WinMergeU.exe" }, new string[4] { "-u", "-e", "\"$REMOTE\"", "\"$LOCAL\"" })
		};

		public static readonly ToolDefinition[] FileEditorToolDefinitions = new ToolDefinition[14]
		{
			new ToolDefinition(ToolType.Antigravity, new string[2] { "%localappdata%\\Programs\\Antigravity\\Antigravity.exe", "%programfiles%\\Google\\Antigravity\\Antigravity.exe" }, new string[2]
			{
				"--goto",
				"$FILEPATH:$LINE:0".Quotify()
			}),
			new ToolDefinition(ToolType.Atom, new string[1] { "%localappdata%\\atom\\atom.exe" }, new string[1] { "$FILEPATH:$LINE".Quotify() }),
			new ToolDefinition(ToolType.Cursor, new string[1] { "%localappdata%\\Programs\\cursor\\Cursor.exe" }, new string[2]
			{
				"--goto",
				"$FILEPATH:$LINE:0".Quotify()
			}),
			new ToolDefinition(ToolType.Fleet, new string[1] { "%localappdata%\\Programs\\Fleet\\Fleet.exe" }, new string[2]
			{
				"--",
				"--goto=$FILEPATH:$LINE".Quotify()
			}),
			new ToolDefinition(ToolType.GoLand, new string[2] { "%programfiles%\\JetBrains\\*\\bin\\goland64.exe", "%localappdata%\\Programs\\GoLand\\bin\\goland64.exe" }, new string[3]
			{
				"--line",
				"$LINE".Quotify(),
				"$FILEPATH".Quotify()
			}),
			new ToolDefinition(ToolType.IntelliJIdea, new string[2] { "%programfiles%\\JetBrains\\*\\bin\\idea64.exe", "%localappdata%\\Programs\\*\\bin\\idea64.exe" }, new string[3]
			{
				"--line",
				"$LINE".Quotify(),
				"$FILEPATH".Quotify()
			}),
			new ToolDefinition(ToolType.PhpStorm, new string[2] { "%programfiles%\\JetBrains\\*\\bin\\phpstorm64.exe", "%localappdata%\\Programs\\PhpStorm\\bin\\phpstorm64.exe" }, new string[3]
			{
				"--line",
				"$LINE".Quotify(),
				"$FILEPATH".Quotify()
			}),
			new ToolDefinition(ToolType.PyCharm, new string[2] { "%programfiles%\\JetBrains\\*\\bin\\pycharm64.exe", "%localappdata%\\Programs\\*\\bin\\pycharm64.exe" }, new string[3]
			{
				"--line",
				"$LINE".Quotify(),
				"$FILEPATH".Quotify()
			}),
			new ToolDefinition(ToolType.Rider, new string[3] { "%programfiles%\\JetBrains\\*\\bin\\rider64.exe", "%localappdata%\\Programs\\Rider\\bin\\rider64.exe", "%localappdata%\\JetBrains\\Installations\\*\\bin\\rider64.exe" }, new string[3]
			{
				"--line",
				"$LINE".Quotify(),
				"$FILEPATH".Quotify()
			}),
			new ToolDefinition(ToolType.SublimeText, new string[5] { "%ProgramW6432%\\Sublime Text\\sublime_text.exe", "%ProgramW6432%\\Sublime Text 3\\sublime_text.exe", "%programfiles%\\Sublime Text 3\\sublime_text.exe", "%ProgramW6432%\\Sublime Text 4\\sublime_text.exe", "%programfiles%\\Sublime Text 4\\sublime_text.exe" }, new string[1] { "$FILEPATH:$LINE".Quotify() }),
			new ToolDefinition(ToolType.VSCode, new string[3] { "%localappdata%\\Programs\\Microsoft VS Code\\Code.exe", "%programfiles(x86)%\\Microsoft VS Code\\Code.exe", "%programfiles%\\Microsoft VS Code\\Code.exe" }, new string[2]
			{
				"--goto",
				"$FILEPATH:$LINE:0".Quotify()
			}),
			new ToolDefinition(ToolType.VSCodeInsiders, new string[3] { "%localappdata%\\Programs\\Microsoft VS Code Insiders\\Code - Insiders.exe", "%programfiles(x86)%\\Microsoft VS Code Insiders\\Code - Insiders.exe", "%programfiles%\\Microsoft VS Code Insiders\\Code - Insiders.exe" }, new string[2]
			{
				"--goto",
				"$FILEPATH:$LINE:0".Quotify()
			}),
			new ToolDefinition(ToolType.WebStorm, new string[2] { "%programfiles%\\JetBrains\\*\\bin\\webstorm64.exe", "%localappdata%\\Programs\\WebStorm\\bin\\webstorm64.exe" }, new string[3]
			{
				"--line",
				"$LINE".Quotify(),
				"$FILEPATH".Quotify()
			}),
			new ToolDefinition(ToolType.Zed, new string[1] { "%localappdata%\\Programs\\Zed\\Zed.exe" }, new string[1] { "$FILEPATH:$LINE".Quotify() })
		};

		public static ExternalTool[] RevealAvailableMergeTools(bool includeNonExistent = false)
		{
			ForkPlus.Settings.ExternalTool[] settingsTools = ForkPlusSettings.Default.ExternalMergeTools ?? new ForkPlus.Settings.ExternalTool[0];
			return RevealAvailableTools(MergeToolDefinitions, settingsTools, includeNonExistent);
		}

		public static void SaveMergeToolsSettings(ExternalTool[] newTools)
		{
			ForkPlusSettings.Default.ExternalMergeTools = ConvertToSettingsTools(newTools, MergeToolDefinitions);
			ForkPlusSettings.Default.Save();
		}

		public static ExternalTool[] RevealAvailableDiffTools(bool includeNonExistent = false)
		{
			ForkPlus.Settings.ExternalTool[] settingsTools = ForkPlusSettings.Default.ExternalDiffTools ?? new ForkPlus.Settings.ExternalTool[0];
			return RevealAvailableTools(DiffToolDefinitions, settingsTools, includeNonExistent);
		}

		public static void SaveDiffToolsSettings(ExternalTool[] newTools)
		{
			ForkPlusSettings.Default.ExternalDiffTools = ConvertToSettingsTools(newTools, DiffToolDefinitions);
			ForkPlusSettings.Default.Save();
		}

		public static ExternalTool[] RevealAvailableFileEditorTools(bool includeNonExistent = false)
		{
			ForkPlus.Settings.ExternalTool[] settingsTools = new ForkPlus.Settings.ExternalTool[0];
			return RevealAvailableTools(FileEditorToolDefinitions, settingsTools, includeNonExistent);
		}

		private static ExternalTool[] RevealAvailableTools(ToolDefinition[] toolDefinitions, ForkPlus.Settings.ExternalTool[] settingsTools, bool includeNonExistent)
		{
			List<ExternalTool> list = new List<ExternalTool>();
			for (int i = 0; i < toolDefinitions.Length; i++)
			{
				ToolDefinition toolDefinition = toolDefinitions[i];
				string[] array = toolDefinition.Paths;
				bool flag = false;
				string[] arguments = toolDefinition.Arguments;
				bool argumentsOverridden = false;
				bool isPrimary = false;
				bool isVisible = true;
				ForkPlus.Settings.ExternalTool externalTool = IReadOnlyListExtensions.FirstItem(settingsTools, (ForkPlus.Settings.ExternalTool x) => x.Type == toolDefinition.Type);
				if (externalTool != null)
				{
					string path = externalTool.Path;
					if (path != null)
					{
						flag = true;
						array = SplitPaths(path);
					}
					string arguments2 = externalTool.Arguments;
					if (arguments2 != null)
					{
						argumentsOverridden = true;
						arguments = ParseArgumentsString(arguments2);
					}
					bool? isPrimary2 = externalTool.IsPrimary;
					if (isPrimary2.HasValue)
					{
						bool valueOrDefault = isPrimary2.GetValueOrDefault();
						isPrimary = valueOrDefault;
					}
					isPrimary2 = externalTool.IsVisible;
					if (isPrimary2.HasValue)
					{
						bool valueOrDefault2 = isPrimary2.GetValueOrDefault();
						isVisible = valueOrDefault2;
					}
				}
				string text = FindExistingInstance(array);
				if (text == null)
				{
					if (!includeNonExistent)
					{
						continue;
					}
					text = (flag ? array.FirstItem() : "");
				}
				list.Add(new ExternalTool(toolDefinition.Type, toolDefinition.FriendlyName, text, flag, arguments, argumentsOverridden, isPredefined: true, isPrimary, isVisible));
			}
			foreach (ForkPlus.Settings.ExternalTool settingsTool in settingsTools)
			{
				if (toolDefinitions.ContainsItem((ToolDefinition x) => x.Type == settingsTool.Type))
				{
					continue;
				}
				if (string.IsNullOrEmpty(settingsTool.Name) || string.IsNullOrEmpty(settingsTool.Path) || string.IsNullOrEmpty(settingsTool.Arguments))
				{
					Log.Warn($"Invalid settings entry for external tool {settingsTool.Type}");
					continue;
				}
				string[] array2 = SplitPaths(settingsTool.Path);
				string text2 = FindExistingInstance(array2);
				if (text2 == null)
				{
					Log.Warn($"Cannot find external tool {settingsTool.Type} at '{settingsTool.Path}'");
					text2 = array2.FirstItem();
				}
				if (text2 != null)
				{
					bool valueOrDefault3 = settingsTool.IsPrimary.GetValueOrDefault();
					bool valueOrDefault4 = settingsTool.IsVisible.GetValueOrDefault(true);
					list.Add(new ExternalTool(settingsTool.Type, settingsTool.Name, text2, pathOverridden: false, ParseArgumentsString(settingsTool.Arguments), argumentsOverridden: false, isPredefined: false, valueOrDefault3, valueOrDefault4));
				}
			}
			list.Sort((ExternalTool x, ExternalTool y) => x.Name.CompareTo(y.Name));
			return list.ToArray();
		}

		private static ForkPlus.Settings.ExternalTool[] ConvertToSettingsTools(ExternalTool[] newTools, ToolDefinition[] toolDefinitions)
		{
			List<ForkPlus.Settings.ExternalTool> list = new List<ForkPlus.Settings.ExternalTool>();
			foreach (ExternalTool item in newTools.Filter((ExternalTool x) => !x.IsPredefined))
			{
				if (string.IsNullOrEmpty(item.Name) || string.IsNullOrEmpty(item.Path))
				{
					continue;
				}
				string text = ArgumentsString(item.Arguments);
				if (!string.IsNullOrEmpty(text))
				{
					bool? isPrimary = null;
					if (item.IsPrimary)
					{
						isPrimary = true;
					}
					bool? isVisible = null;
					if (!item.IsVisible)
					{
						isVisible = false;
					}
					list.Add(new ForkPlus.Settings.ExternalTool(ToolType.Custom, item.Name, item.Path, text, isPrimary, isVisible));
				}
			}
			foreach (ExternalTool tool in newTools.Filter((ExternalTool x) => x.IsPredefined))
			{
				ToolDefinition? toolDefinition = toolDefinitions.FirstItemStruct((ToolDefinition x) => x.Type == tool.Type);
				if (toolDefinition.HasValue)
				{
					toolDefinition.GetValueOrDefault();
					string text2 = (tool.PathOverridden ? tool.Path : null);
					string[] array = (tool.ArgumentsOverridden ? tool.Arguments : null);
					bool? isPrimary2 = null;
					if (tool.IsPrimary)
					{
						isPrimary2 = true;
					}
					bool? isVisible2 = null;
					if (!tool.IsVisible)
					{
						isVisible2 = false;
					}
					if (text2 != null || array != null || isPrimary2.HasValue || isVisible2.HasValue)
					{
						list.Add(new ForkPlus.Settings.ExternalTool(tool.Type, null, text2, ArgumentsString(array), isPrimary2, isVisible2));
					}
				}
				else
				{
					Log.Warn("Invalid predefined tool " + tool.Name);
				}
			}
			return list.ToArray();
		}

		[Null]
		public static string GetPredefinedToolPath(ToolDefinition toolDefinition)
		{
			return FindExistingInstance(toolDefinition.Paths);
		}

		[Null]
		private static string FindExistingInstance(string[] patterns)
		{
			foreach (string text in patterns)
			{
				try
				{
					string text2 = Environment.ExpandEnvironmentVariables(text);
					string[] array = text2.Split(new string[1] { "*\\" }, StringSplitOptions.None);
					if (array.Length == 1)
					{
						if (File.Exists(text2))
						{
							return text2;
						}
						continue;
					}
					if (array.Length == 2)
					{
						string path = array[0];
						string path2 = array[1];
						if (!Directory.Exists(path))
						{
							continue;
						}
						string[] directories = Directory.GetDirectories(path);
						Array.Sort(directories, (string x, string y) => -1 * x.CompareTo(y));
						string[] array2 = directories;
						for (int j = 0; j < array2.Length; j++)
						{
							string text3 = Path.Combine(array2[j], path2);
							if (File.Exists(text3))
							{
								return text3;
							}
						}
						continue;
					}
					return null;
				}
				catch (Exception ex)
				{
					Log.Error("Failed to find external tool in '" + text + "'", ex);
				}
			}
			return null;
		}

		private static string[] SplitPaths(string pathString)
		{
			return pathString.Split(Consts.Chars.Semicolon).Map((string x) => x.Trim());
		}

		private static string[] ParseArgumentsString(string argumentsString)
		{
			return argumentsString.Split(Consts.Chars.Space);
		}

		[Null]
		private static string ArgumentsString(string[] arguments)
		{
			if (arguments != null)
			{
				return string.Join(" ", arguments);
			}
			return null;
		}
	}
}
