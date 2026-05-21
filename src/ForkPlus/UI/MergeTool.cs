using System;
using System.IO;

namespace ForkPlus.UI
{
	public abstract class MergeTool
	{
		public class Custom : MergeTool
		{
			private string _applicationPath;

			private string _arguments;

			public override string FriendlyName
			{
				get
				{
					if (_applicationPath == "")
					{
						return null;
					}
					try
					{
						return Path.GetFileNameWithoutExtension(_applicationPath);
					}
					catch (Exception arg)
					{
						Log.Warn($"Failed to get name of custom merge tool: {arg}");
						return "Custom";
					}
				}
			}

			public override string Type => "Custom";

			public override string ApplicationPath => _applicationPath;

			public override string Arguments => _arguments;

			public override string DiffArguments => _arguments;

			public Custom(string applicationPath, string arguments)
			{
				_applicationPath = applicationPath;
				_arguments = arguments;
			}
		}

		public class Cursor : MergeTool
		{
			private string _applicationPath;

			public override string FriendlyName => "Cursor";

			public override string Type => "Cursor";

			public override string ApplicationPath => _applicationPath;

			public override string Arguments => "-n --wait --merge \"$REMOTE\" \"$LOCAL\" \"$BASE\" \"$MERGED\"";

			public override string DiffArguments => "--diff --wait \"$REMOTE\" \"$LOCAL\"";

			public static string TryFindInstance()
			{
				return FindExistingInstance(new string[1] { "%localappdata%\\Programs\\cursor\\Cursor.exe" });
			}

			public Cursor(string applicationPath)
			{
				_applicationPath = applicationPath;
			}
		}

		public class VisualStudio : MergeTool
		{
			private string _applicationPath;

			public override string FriendlyName => "Visual Studio";

			public override string Type => "VisualStudio";

			public override string ApplicationPath => _applicationPath;

			public override string Arguments => "\"$REMOTE\" \"$LOCAL\" \"$BASE\" \"$MERGED\" /m";

			public override string DiffArguments => "\"$REMOTE\" \"$LOCAL\" /t";

			public static string TryFindInstance()
			{
				return FindExistingInstance(new string[3]
				{
					Consts.Env.ProgramFiles + "\\Microsoft Visual Studio\\2022\\Community\\Common7\\IDE\\CommonExtensions\\Microsoft\\TeamFoundation\\Team Explorer\\vsDiffMerge.exe",
					Consts.Env.ProgramFiles86 + "\\Microsoft Visual Studio\\2019\\Community\\Common7\\IDE\\CommonExtensions\\Microsoft\\TeamFoundation\\Team Explorer\\vsDiffMerge.exe",
					Consts.Env.ProgramFiles86 + "\\Microsoft Visual Studio\\2017\\Community\\Common7\\IDE\\CommonExtensions\\Microsoft\\TeamFoundation\\Team Explorer\\vsDiffMerge.exe"
				});
			}

			public VisualStudio(string applicationPath)
			{
				_applicationPath = applicationPath;
			}
		}

		public class VSCode : MergeTool
		{
			private string _applicationPath;

			public override string FriendlyName => "VS Code";

			public override string Type => "VSCode";

			public override string ApplicationPath => _applicationPath;

			public override string Arguments => "-n --wait --merge \"$REMOTE\" \"$LOCAL\" \"$BASE\" \"$MERGED\"";

			public override string DiffArguments => "--diff --wait \"$REMOTE\" \"$LOCAL\"";

			public static string TryFindInstance()
			{
				return FindExistingInstance(new string[3] { "%localappdata%\\Programs\\Microsoft VS Code\\Code.exe", "%programfiles(x86)%\\Microsoft VS Code\\Code.exe", "%programfiles%\\Microsoft VS Code\\Code.exe" });
			}

			public VSCode(string applicationPath)
			{
				_applicationPath = applicationPath;
			}
		}

		public class AraxisMerge : MergeTool
		{
			private string _applicationPath;

			public override string FriendlyName => "Araxis Merge";

			public override string Type => "AraxisMerge";

			public override string ApplicationPath => _applicationPath;

			public override string Arguments => "-wait -merge -3 -a1 \"$BASE\" \"$REMOTE\" \"$LOCAL\" \"$MERGED\"";

			public override string DiffArguments => "\"$REMOTE\" \"$LOCAL\"";

			public AraxisMerge(string applicationPath)
			{
				_applicationPath = applicationPath;
			}

			public static string TryFindInstance()
			{
				return FindExistingInstance(new string[3] { "%localappdata%\\Apps\\Araxis\\Araxis Merge\\compare.exe", "%ProgramW6432%\\Araxis\\Araxis Merge\\compare.exe", "%programfiles(x86)%\\Araxis\\Araxis Merge\\compare.exe" });
			}
		}

		public class BeyondCompare : MergeTool
		{
			private string _applicationPath;

			public override string FriendlyName => "Beyond Compare";

			public override string Type => "BeyondCompare";

			public override string ApplicationPath => _applicationPath;

			public override string Arguments => "\"$REMOTE\" \"$LOCAL\" \"$BASE\" \"$MERGED\"";

			public override string DiffArguments => "\"$REMOTE\" \"$LOCAL\"";

			public BeyondCompare(string applicationPath)
			{
				_applicationPath = applicationPath;
			}

			public static string TryFindInstance()
			{
				return FindExistingInstance(new string[4] { "%ProgramW6432%\\Beyond Compare 5\\BCompare.exe", "%programfiles(x86)%\\Beyond Compare 5\\BCompare.exe", "%ProgramW6432%\\Beyond Compare 4\\BCompare.exe", "%programfiles(x86)%\\Beyond Compare 4\\BCompare.exe" });
			}
		}

		public class KDiff3 : MergeTool
		{
			private string _applicationPath;

			public override string FriendlyName => "KDiff";

			public override string Type => "KDiff3";

			public override string ApplicationPath => _applicationPath;

			public override string Arguments => "\"$REMOTE\" -b \"$BASE\" \"$LOCAL\" -o \"$MERGED\"";

			public override string DiffArguments => "\"$REMOTE\" \"$LOCAL\"";

			public KDiff3(string applicationPath)
			{
				_applicationPath = applicationPath;
			}

			public static string TryFindInstance()
			{
				return FindExistingInstance(new string[4] { "%ProgramW6432%\\KDiff3\\kdiff3.exe", "%programfiles%\\KDiff3\\kdiff3.exe", "%ProgramW6432%\\KDiff3\\bin\\kdiff3.exe", "%programfiles%\\KDiff3\\bin\\kdiff3.exe" });
			}
		}

		public class P4Merge : MergeTool
		{
			private string _applicationPath;

			public override string FriendlyName => "P4Merge";

			public override string Type => "P4Merge";

			public override string ApplicationPath => _applicationPath;

			public override string Arguments => "\"$BASE\" \"$REMOTE\" \"$LOCAL\" \"$MERGED\"";

			public override string DiffArguments => "\"$REMOTE\" \"$LOCAL\"";

			public P4Merge(string applicationPath)
			{
				_applicationPath = applicationPath;
			}

			public static string TryFindInstance()
			{
				return FindExistingInstance(new string[2] { "%ProgramW6432%\\Perforce\\p4merge.exe", "%programfiles%\\Perforce\\p4merge.exe" });
			}
		}

		public class Unity3d : MergeTool
		{
			private string _applicationPath;

			public override string FriendlyName => "YAMLMerge";

			public override string Type => "UnityYAMLMerge";

			public override string ApplicationPath => _applicationPath;

			public override string Arguments => "merge -p \"$BASE\" \"$REMOTE\" \"$LOCAL\" \"$MERGED\"";

			public override string DiffArguments => "";

			public Unity3d(string applicationPath)
			{
				_applicationPath = applicationPath;
			}

			public static string TryFindInstance()
			{
				return FindExistingInstance(new string[2] { "%ProgramW6432%\\Unity\\Hub\\Editor\\2019.4.16f1\\Editor\\Data\\Tools\\UnityYAMLMerge.exe", "%programfiles%\\Unity\\Hub\\Editor\\2019.4.16f1\\Editor\\Data\\Tools\\UnityYAMLMerge.exe" });
			}
		}

		public class WinMerge : MergeTool
		{
			private string _applicationPath;

			public override string FriendlyName => "WinMerge";

			public override string Type => "WinMerge";

			public override string ApplicationPath => _applicationPath;

			public override string Arguments => "-u -e \"$REMOTE\" \"$BASE\" \"$LOCAL\" -o \"$MERGED\"";

			public override string DiffArguments => "-u -e \"$REMOTE\" \"$LOCAL\"";

			public WinMerge(string applicationPath)
			{
				_applicationPath = applicationPath;
			}

			public static string TryFindInstance()
			{
				return FindExistingInstance(new string[2] { "%ProgramW6432%\\WinMerge\\WinMergeU.exe", "%programfiles%\\WinMerge\\WinMergeU.exe" });
			}
		}

		public const string CustomType = "Custom";

		public const string AraxisMergeType = "AraxisMerge";

		public const string BeyondCompareType = "BeyondCompare";

		public const string CursorType = "Cursor";

		public const string KDiff3Type = "KDiff3";

		public const string P4MergeType = "P4Merge";

		public const string VisualStudioType = "VisualStudio";

		public const string VSCodeType = "VSCode";

		public const string Unity3dType = "UnityYAMLMerge";

		public const string WinMergeType = "WinMerge";

		public abstract string FriendlyName { get; }

		public abstract string ApplicationPath { get; }

		public abstract string Arguments { get; }

		public abstract string DiffArguments { get; }

		public abstract string Type { get; }

		protected static string FindExistingInstance(string[] possiblePaths)
		{
			foreach (string text in possiblePaths)
			{
				try
				{
					string text2 = Environment.ExpandEnvironmentVariables(text);
					if (File.Exists(text2))
					{
						return text2;
					}
				}
				catch (Exception ex)
				{
					Log.Error("Failed to find merge tool in '" + text + "'", ex);
				}
			}
			return null;
		}
	}
}
