using System;
using System.IO;

namespace ForkPlus.UI
{
	public abstract class ShellTool
	{
		public class Default : ShellTool
		{
			public override string Type => DefaultType;

			public override string DisplayName => "Console";

			public override string Arguments => null;

			public override string ApplicationPath
			{
				get
				{
					string directoryName = Path.GetDirectoryName(App.GitPath);
					if (directoryName == null)
					{
						Log.Error("Cannot find git directory '" + directoryName + "'");
						return null;
					}
					return Path.Combine(directoryName, Consts.ForkPlus.BashFilename);
				}
			}
		}

		public class Custom : ShellTool
		{
			public override string Type => CustomType;

			public override string DisplayName => "Shell";

			public override string Arguments { get; }

			public override string ApplicationPath { get; }

			public Custom(string applicationPath, [Null] string arguments)
			{
				ApplicationPath = applicationPath;
				Arguments = arguments;
			}
		}

		public class WindowsTerminal : ShellTool
		{
			public override string Type => WindowsTerminalType;

			public override string DisplayName => "Terminal";

			public override string ApplicationPath { get; }

			public override string Arguments => "-d .";

			public WindowsTerminal(string applicationPath)
			{
				ApplicationPath = applicationPath;
			}

			public static string TryFindInstance()
			{
				return FindExistingInstance(new string[1] { "%localappdata%\\Microsoft\\WindowsApps\\wt.exe" });
			}
		}

		public class CommandPrompt : ShellTool
		{
			public override string Type => CommandPromptType;

			public override string DisplayName => "Console";

			public override string ApplicationPath { get; }

			public override string Arguments => null;

			public CommandPrompt(string applicationPath)
			{
				ApplicationPath = applicationPath;
			}

			public static string TryFindInstance()
			{
				return FindExistingInstance(new string[1] { "%windir%\\System32\\cmd.exe" });
			}
		}

		public class PowerShell : ShellTool
		{
			public override string Type => PowerShellType;

			public override string DisplayName => "PowerShell";

			public override string ApplicationPath { get; }

			public override string Arguments => null;

			public PowerShell(string applicationPath)
			{
				ApplicationPath = applicationPath;
			}

			public static string TryFindInstance()
			{
				return FindExistingInstance(new string[1] { "%windir%\\System32\\WindowsPowerShell\\v1.0\\powershell.exe" });
			}
		}

		public static readonly string DefaultType = "Default";

		public static readonly string CustomType = "Custom";

		public static readonly string WindowsTerminalType = "WindowsTerminal";

		public static readonly string CommandPromptType = "CommandPrompt";

		public static readonly string PowerShellType = "PowerShell";

		public abstract string Type { get; }

		public abstract string DisplayName { get; }

		public abstract string ApplicationPath { get; }

		public abstract string Arguments { get; }

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
					Log.Error("Failed to check if '" + text + "' exists", ex);
				}
			}
			return null;
		}
	}
}
