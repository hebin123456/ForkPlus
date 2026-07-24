// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows.Media → using Avalonia.Media
// - ImageSource → IImage
using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Media;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI
{
	public abstract class ExternalRepositoryEditor
	{
		public class Antigravity : ExternalRepositoryEditor
		{
			public override string Name => "Antigravity";

			public override string ApplicationPath { get; }

			public override IImage Icon { get; }

			[Null]
			public static string TryFindInstance()
			{
				return FindExistingInstance(new string[2] { "%localappdata%\\Programs\\Antigravity\\Antigravity.exe", "%programfiles%\\Google\\Antigravity\\Antigravity.exe" });
			}

			public Antigravity(string applicationPath)
			{
				ApplicationPath = applicationPath;
				Icon = IconTools.GetImageSourceForFile(applicationPath);
			}
		}

		public class Cursor : ExternalRepositoryEditor
		{
			public override string Name => "Cursor";

			public override string ApplicationPath { get; }

			public override IImage Icon { get; }

			[Null]
			public static string TryFindInstance()
			{
				return FindExistingInstance(new string[2] { "%localappdata%\\Programs\\cursor\\Cursor.exe", "%programfiles%\\cursor\\Cursor.exe" });
			}

			public Cursor(string applicationPath)
			{
				ApplicationPath = applicationPath;
				Icon = IconTools.GetImageSourceForFile(applicationPath);
			}
		}

		public class Fleet : ExternalRepositoryEditor
		{
			public override string Name => "Fleet";

			public override string ApplicationPath { get; }

			public override IImage Icon { get; }

			[Null]
			public static string TryFindInstance()
			{
				return FindExistingInstance(new string[1] { "%localappdata%\\Programs\\Fleet\\Fleet.exe" });
			}

			public Fleet(string applicationPath)
			{
				ApplicationPath = applicationPath;
				Icon = IconTools.GetImageSourceForFile(applicationPath);
			}
		}

		public class OpenCode : ExternalRepositoryEditor
		{
			public override string Name => "OpenCode";

			public override string ApplicationPath { get; }

			public override IImage Icon { get; }

			[Null]
			public static string TryFindInstance()
			{
				return FindExistingInstance(new string[]
				{
					"%appdata%\\npm\\opencode.cmd",
					"%appdata%\\npm\\OpenCode.cmd",
					"%appdata%\\npm\\opencode.exe",
					"%appdata%\\npm\\OpenCode.exe",
				}) ?? FindExecutableInPath("opencode") ?? FindExecutableInPath("OpenCode");
			}

			public OpenCode(string applicationPath)
			{
				ApplicationPath = applicationPath;
				Icon = IconTools.GetImageSourceForFile(applicationPath);
			}
		}

		public class VSCode : ExternalRepositoryEditor
		{
			public override string Name => "Visual Studio Code";

			public override string ApplicationPath { get; }

			public override IImage Icon { get; }

			[Null]
			public static string TryFindInstance()
			{
				return FindExistingInstance(new string[3] { "%localappdata%\\Programs\\Microsoft VS Code\\Code.exe", "%programfiles(x86)%\\Microsoft VS Code\\Code.exe", "%ProgramW6432%\\Microsoft VS Code\\Code.exe" });
			}

			public VSCode(string applicationPath)
			{
				ApplicationPath = applicationPath;
				Icon = IconTools.GetImageSourceForFile(applicationPath);
			}
		}

		public class VSCodeInsiders : ExternalRepositoryEditor
		{
			public override string Name => "Visual Studio Code Insiders";

			public override string ApplicationPath { get; }

			public override IImage Icon { get; }

			[Null]
			public static string TryFindInstance()
			{
				return FindExistingInstance(new string[3] { "%localappdata%\\Programs\\Microsoft VS Code Insiders\\Code - Insiders.exe", "%programfiles(x86)%\\Microsoft VS Code Insiders\\Code - Insiders.exe", "%programfiles%\\Microsoft VS Code Insiders\\Code - Insiders.exe" });
			}

			public VSCodeInsiders(string applicationPath)
			{
				ApplicationPath = applicationPath;
				Icon = IconTools.GetImageSourceForFile(applicationPath);
			}
		}

		public class SublimeText : ExternalRepositoryEditor
		{
			public override string Name => "Sublime Text";

			public override string ApplicationPath { get; }

			public override IImage Icon { get; }

			[Null]
			public static string TryFindInstance()
			{
				return FindExistingInstance(new string[5] { "%ProgramW6432%\\Sublime Text\\sublime_text.exe", "%ProgramW6432%\\Sublime Text 3\\sublime_text.exe", "%programfiles%\\Sublime Text 3\\sublime_text.exe", "%ProgramW6432%\\Sublime Text 4\\sublime_text.exe", "%programfiles%\\Sublime Text 4\\sublime_text.exe" });
			}

			public SublimeText(string applicationPath)
			{
				ApplicationPath = applicationPath;
				Icon = IconTools.GetImageSourceForFile(applicationPath);
			}
		}

		public class Atom : ExternalRepositoryEditor
		{
			public override string Name => "Atom";

			public override string ApplicationPath { get; }

			public override IImage Icon { get; }

			[Null]
			public static string TryFindInstance()
			{
				return FindExistingInstance(new string[1] { "%localappdata%\\atom\\atom.exe" });
			}

			public Atom(string applicationPath)
			{
				ApplicationPath = applicationPath;
				Icon = IconTools.GetImageSourceForFile(applicationPath);
			}
		}

		public class WebStorm : ExternalRepositoryEditor
		{
			public override string Name => "WebStorm";

			public override string ApplicationPath { get; }

			public override IImage Icon { get; }

			[Null]
			public static string TryFindInstance()
			{
				return FindExistingInstance(new string[2] { "%programfiles%\\JetBrains\\*\\bin\\webstorm64.exe", "%localappdata%\\Programs\\WebStorm\\bin\\webstorm64.exe" });
			}

			public WebStorm(string applicationPath)
			{
				ApplicationPath = applicationPath;
				Icon = IconTools.GetImageSourceForFile(applicationPath);
			}
		}

		public class Zed : ExternalRepositoryEditor
		{
			public override string Name => "Zed";

			public override string ApplicationPath { get; }

			public override IImage Icon { get; }

			[Null]
			public static string TryFindInstance()
			{
				return FindExistingInstance(new string[1] { "%localappdata%\\Programs\\Zed\\Zed.exe" });
			}

			public Zed(string applicationPath)
			{
				ApplicationPath = applicationPath;
				Icon = IconTools.GetImageSourceForFile(applicationPath);
			}
		}

		public abstract string Name { get; }

		public abstract string ApplicationPath { get; }

		public abstract IImage Icon { get; }

		public static ExternalRepositoryEditor[] GetAvailableEditors()
		{
			List<ExternalRepositoryEditor> list = new List<ExternalRepositoryEditor>(5);
			string text = Antigravity.TryFindInstance();
			if (text != null)
			{
				list.Add(new Antigravity(text));
			}
			string text2 = Atom.TryFindInstance();
			if (text2 != null)
			{
				list.Add(new Atom(text2));
			}
			string text3 = Cursor.TryFindInstance();
			if (text3 != null)
			{
				list.Add(new Cursor(text3));
			}
			string text4 = Fleet.TryFindInstance();
			if (text4 != null)
			{
				list.Add(new Fleet(text4));
			}
			string openCode = OpenCode.TryFindInstance();
			if (openCode != null)
			{
				list.Add(new OpenCode(openCode));
			}
			string text5 = SublimeText.TryFindInstance();
			if (text5 != null)
			{
				list.Add(new SublimeText(text5));
			}
			string text6 = VSCode.TryFindInstance();
			if (text6 != null)
			{
				list.Add(new VSCode(text6));
			}
			string text7 = VSCodeInsiders.TryFindInstance();
			if (text7 != null)
			{
				list.Add(new VSCodeInsiders(text7));
			}
			string text8 = WebStorm.TryFindInstance();
			if (text8 != null)
			{
				list.Add(new WebStorm(text8));
			}
			string text9 = Zed.TryFindInstance();
			if (text9 != null)
			{
				list.Add(new Zed(text9));
			}
			return list.ToArray();
		}

		[Null]
		protected static string FindExistingInstance(string[] patterns)
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
					Log.Error("Failed to findind existing instance for '" + text + "'", ex);
				}
			}
			return null;
		}

		[Null]
		protected static string FindExecutableInPath(string executableName)
		{
			string pathValue = Environment.GetEnvironmentVariable("PATH");
			if (string.IsNullOrWhiteSpace(pathValue))
			{
				return null;
			}
			string[] extensions = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD").Split(new char[1] { ';' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string directory in pathValue.Split(Path.PathSeparator))
			{
				if (string.IsNullOrWhiteSpace(directory))
				{
					continue;
				}
				string trimmedDirectory = directory.Trim();
				foreach (string extension in extensions)
				{
					string candidate = Path.Combine(trimmedDirectory, executableName + extension.ToLowerInvariant());
					if (File.Exists(candidate))
					{
						return candidate;
					}
					candidate = Path.Combine(trimmedDirectory, executableName + extension.ToUpperInvariant());
					if (File.Exists(candidate))
					{
						return candidate;
					}
				}
				string candidateWithoutExtension = Path.Combine(trimmedDirectory, executableName);
				if (File.Exists(candidateWithoutExtension))
				{
					return candidateWithoutExtension;
				}
			}
			return null;
		}
	}
}
