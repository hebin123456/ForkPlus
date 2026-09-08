using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Avalonia.Media;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI
{
	public abstract class ExternalProjectEditor
	{
		public class IntelliJIdea : ExternalProjectEditor
		{
			public override string Name => "IntelliJ IDEA";

			public override string ApplicationPath { get; }

			public override global::Avalonia.Media.IImage Icon { get; }

			// v3.9.1：IDEA 适合 Java 系（Maven/Gradle）项目
			public override ProjectType RecommendedProjectTypes => ProjectType.Maven | ProjectType.Gradle;

			[Null]
			public static string TryFindInstance()
			{
				return FindExistingInstance(new string[2] { "%programfiles%\\JetBrains\\*\\bin\\idea64.exe", "%localappdata%\\Programs\\*\\bin\\idea64.exe" });
			}

			public IntelliJIdea(string applicationPath)
			{
				ApplicationPath = applicationPath;
				Icon = IconTools.GetImageSourceForFile(applicationPath);
			}

			public override string[] GetProjectFilePaths(string gitDirectory)
			{
				List<string> list = new List<string>();
				try
				{
					if (Directory.Exists(Path.Combine(gitDirectory, ".idea")))
					{
						list.Add(gitDirectory);
					}
				}
				catch (Exception ex)
				{
					Log.Error(ex.Message);
				}
				return list.ToArray();
			}
		}

		public class GoLand : IntelliJIdea
		{
			public override string Name => "GoLand";

			// v3.9.1：GoLand 适合 Go 项目
			public override ProjectType RecommendedProjectTypes => ProjectType.Go;

			[Null]
			public new static string TryFindInstance()
			{
				return FindExistingInstance(new string[2] { "%programfiles%\\JetBrains\\*\\bin\\goland64.exe", "%localappdata%\\Programs\\GoLand\\bin\\goland64.exe" });
			}

			public GoLand(string applicationPath)
				: base(applicationPath)
			{
			}
		}

		public class PhpStorm : IntelliJIdea
		{
			public override string Name => "PhpStorm";

			// v3.9.1：PhpStorm 适合 PHP 项目
			public override ProjectType RecommendedProjectTypes => ProjectType.Php;

			[Null]
			public new static string TryFindInstance()
			{
				return FindExistingInstance(new string[2] { "%programfiles%\\JetBrains\\*\\bin\\phpstorm64.exe", "%localappdata%\\Programs\\PhpStorm\\bin\\phpstorm64.exe" });
			}

			public PhpStorm(string applicationPath)
				: base(applicationPath)
			{
			}
		}

		public class PyCharm : IntelliJIdea
		{
			public override string Name => "PyCharm";

			// v3.9.1：PyCharm 适合 Python 项目
			public override ProjectType RecommendedProjectTypes => ProjectType.Python;

			[Null]
			public new static string TryFindInstance()
			{
				return FindExistingInstance(new string[2] { "%programfiles%\\JetBrains\\*\\bin\\pycharm64.exe", "%localappdata%\\Programs\\*\\bin\\pycharm64.exe" });
			}

			public PyCharm(string applicationPath)
				: base(applicationPath)
			{
			}
		}

		public class Rider : ExternalProjectEditor
		{
			public override string Name => "Rider";

			public override string ApplicationPath { get; }

			public override global::Avalonia.Media.IImage Icon { get; }

			protected override string[] ProjectExtensions => new string[5] { "*.sln", "*.slnf", "*.slnx", "*.csproj", "*.uproject" };

			// v3.9.1：Rider 适合 .NET 项目
			public override ProjectType RecommendedProjectTypes => ProjectType.DotNet;

			[Null]
			public static string TryFindInstance()
			{
				return FindExistingInstance(new string[3] { "%programfiles%\\JetBrains\\*\\bin\\rider64.exe", "%localappdata%\\Programs\\Rider\\bin\\rider64.exe", "%localappdata%\\JetBrains\\Installations\\*\\bin\\rider64.exe" });
			}

			public Rider(string applicationPath)
			{
				ApplicationPath = applicationPath;
				Icon = IconTools.GetImageSourceForFile(applicationPath);
			}

			public override string[] GetProjectFilePaths(string gitDirectory)
			{
				string[] projectFilePaths = base.GetProjectFilePaths(gitDirectory);
				if (projectFilePaths.AnyItem((string x) => x.EndsWith(".sln") || x.EndsWith(".slnx")))
				{
					return projectFilePaths.Filter((string x) => !x.EndsWith(".csproj")).ToArray();
				}
				return projectFilePaths;
			}
		}

		public class VisualStudio : ExternalProjectEditor
		{
			public override string Name => "Visual Studio";

			public override string ApplicationPath { get; }

			public override global::Avalonia.Media.IImage Icon { get; }

			protected override string[] ProjectExtensions => new string[3] { "*.sln", "*.slnf", "*.slnx" };

			// v3.9.1：VS 适合 .NET 项目
			public override ProjectType RecommendedProjectTypes => ProjectType.DotNet;

			[Null]
			public static string TryFindInstance()
			{
				return FindExistingInstance(new string[13]
				{
					Consts.Env.ProgramFiles86 + "\\Common Files\\Microsoft Shared\\MSEnv\\VSLauncher.exe",
					Consts.Env.ProgramFiles + "\\Microsoft Visual Studio\\18\\Enterprise\\Common7\\IDE\\devenv.exe",
					Consts.Env.ProgramFiles + "\\Microsoft Visual Studio\\18\\Professional\\Common7\\IDE\\devenv.exe",
					Consts.Env.ProgramFiles + "\\Microsoft Visual Studio\\18\\Community\\Common7\\IDE\\devenv.exe",
					Consts.Env.ProgramFiles + "\\Microsoft Visual Studio\\2022\\Enterprise\\Common7\\IDE\\devenv.exe",
					Consts.Env.ProgramFiles + "\\Microsoft Visual Studio\\2022\\Professional\\Common7\\IDE\\devenv.exe",
					Consts.Env.ProgramFiles + "\\Microsoft Visual Studio\\2022\\Community\\Common7\\IDE\\devenv.exe",
					Consts.Env.ProgramFiles86 + "\\Microsoft Visual Studio\\2019\\Enterprise\\Common7\\IDE\\devenv.exe",
					Consts.Env.ProgramFiles86 + "\\Microsoft Visual Studio\\2019\\Professional\\Common7\\IDE\\devenv.exe",
					Consts.Env.ProgramFiles86 + "\\Microsoft Visual Studio\\2019\\Community\\Common7\\IDE\\devenv.exe",
					Consts.Env.ProgramFiles86 + "\\Microsoft Visual Studio\\2017\\Enterprise\\Common7\\IDE\\devenv.exe",
					Consts.Env.ProgramFiles86 + "\\Microsoft Visual Studio\\2017\\Professional\\Common7\\IDE\\devenv.exe",
					Consts.Env.ProgramFiles86 + "\\Microsoft Visual Studio\\2017\\Community\\Common7\\IDE\\devenv.exe"
				});
			}

			public VisualStudio(string applicationPath)
			{
				ApplicationPath = applicationPath;
				Icon = IconTools.GetImageSourceForFile(applicationPath);
			}
		}

		/// <summary>v3.9.1：Android Studio。Android 项目的推荐 IDE。</summary>
		public class AndroidStudio : ExternalProjectEditor
		{
			public override string Name => "Android Studio";

			public override string ApplicationPath { get; }

			public override global::Avalonia.Media.IImage Icon { get; }

			// v3.9.1：Android Studio 适合 Android 项目
			public override ProjectType RecommendedProjectTypes => ProjectType.Android;

			[Null]
			public static string TryFindInstance()
			{
				return FindExistingInstance(new string[3]
				{
					"%programfiles%\\Android\\Android Studio\\bin\\studio64.exe",
					"%localappdata%\\Programs\\Android Studio\\bin\\studio64.exe",
					"%programfiles%\\Android\\*\\bin\\studio64.exe"
				});
			}

			public AndroidStudio(string applicationPath)
			{
				ApplicationPath = applicationPath;
				Icon = IconTools.GetImageSourceForFile(applicationPath);
			}

			// Android Studio 直接打开仓库根目录即可自动识别 gradle 配置
			public override string[] GetProjectFilePaths(string gitDirectory)
			{
				return new string[1] { gitDirectory };
			}
		}

		public abstract string Name { get; }

		public abstract string ApplicationPath { get; }

		public abstract global::Avalonia.Media.IImage Icon { get; }

		protected virtual string[] ProjectExtensions => new string[0];

		/// <summary>v3.9.1：该 IDE 适合的项目类型。基类默认 Unknown 表示"通用"（不过滤）。
		/// 子类覆盖以声明适合的项目类型，用于"Open in"菜单智能过滤。</summary>
		public virtual ProjectType RecommendedProjectTypes => ProjectType.Unknown;

		public static ExternalProjectEditor[] GetAvailableEditors()
		{
			List<ExternalProjectEditor> list = new List<ExternalProjectEditor>(2);
			// v3.9.1：新增 AndroidStudio
			string androidStudio = AndroidStudio.TryFindInstance();
			if (androidStudio != null)
			{
				list.Add(new AndroidStudio(androidStudio));
			}
			string text = GoLand.TryFindInstance();
			if (text != null)
			{
				list.Add(new GoLand(text));
			}
			string text2 = IntelliJIdea.TryFindInstance();
			if (text2 != null)
			{
				list.Add(new IntelliJIdea(text2));
			}
			string text3 = PhpStorm.TryFindInstance();
			if (text3 != null)
			{
				list.Add(new PhpStorm(text3));
			}
			string text4 = PyCharm.TryFindInstance();
			if (text4 != null)
			{
				list.Add(new PyCharm(text4));
			}
			string text5 = Rider.TryFindInstance();
			if (text5 != null)
			{
				list.Add(new Rider(text5));
			}
			string text6 = VisualStudio.TryFindInstance();
			if (text6 != null)
			{
				list.Add(new VisualStudio(text6));
			}
			return list.ToArray();
		}

		public virtual string[] GetProjectFilePaths(string gitDirectory)
		{
			List<string> list = new List<string>();
			try
			{
				string[] projectExtensions = ProjectExtensions;
				foreach (string text in projectExtensions)
				{
					string[] files = Directory.GetFiles(gitDirectory, text, SearchOption.TopDirectoryOnly);
					foreach (string text2 in files)
					{
						if (text2.EndsWith(text.TrimStart("*")))
						{
							list.Add(text2);
						}
					}
					string path = Path.Combine(gitDirectory, "src");
					if (!Directory.Exists(path))
					{
						continue;
					}
					files = Directory.GetFiles(path, text, SearchOption.TopDirectoryOnly);
					foreach (string text3 in files)
					{
						if (text3.EndsWith(text.TrimStart("*")))
						{
							list.Add(text3);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error(ex.Message);
			}
			return list.ToArray();
		}

		public void OpenProject(string absoluteProjectFilePath)
		{
			StartProcess(ApplicationPath, new string[1] { absoluteProjectFilePath });
		}

		protected static void StartProcess(string path, string[] arguments)
		{
			string text = string.Join(" ", arguments);
			Process process = new Process();
			process.StartInfo = new ProcessStartInfo
			{
				FileName = path,
				Arguments = text.Quotify()
			};
			Log.Info("Running External Project Editor '" + path + " " + text + "'");
			try
			{
				process.Start();
			}
			catch (Exception ex)
			{
				Log.Error("Failed to run extenrnal project editor '" + path + " " + text + "'", ex);
			}
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
					Log.Error("Failed to find project editor instance for '" + text + "'", ex);
				}
			}
			return null;
		}
	}
}
