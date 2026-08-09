using System;
using System.IO;
using System.Linq;

namespace ForkPlus
{
	/// <summary>
	/// v3.9.1：扫描仓库根目录的标志文件，识别项目类型（Node/Maven/Android/Python/Go/.NET/PHP）。
	/// 简单优先：只扫根目录，不递归子目录。一个仓库可识别出多种类型。
	/// </summary>
	public static class ProjectTypeDetector
	{
		/// <summary>扫描 git 仓库根目录，返回项目类型组合（Flags）。</summary>
		public static ProjectType Detect(string gitDirectory)
		{
			if (string.IsNullOrEmpty(gitDirectory) || !Directory.Exists(gitDirectory))
			{
				return ProjectType.Unknown;
			}

			ProjectType type = ProjectType.Unknown;
			try
			{
				// Android 必须先判断（依赖 build.gradle + AndroidManifest）
				// Node
				if (File.Exists(Path.Combine(gitDirectory, "package.json")))
				{
					type |= ProjectType.Node;
				}
				// Maven
				if (File.Exists(Path.Combine(gitDirectory, "pom.xml")))
				{
					type |= ProjectType.Maven;
				}
				// Gradle（含 Android）
				string buildGradle = Path.Combine(gitDirectory, "build.gradle");
				string buildGradleKts = Path.Combine(gitDirectory, "build.gradle.kts");
				bool hasGradle = File.Exists(buildGradle) || File.Exists(buildGradleKts);
				if (hasGradle)
				{
					// Android：根目录或 app/src/main 下有 AndroidManifest.xml
					bool isAndroid = File.Exists(Path.Combine(gitDirectory, "src", "main", "AndroidManifest.xml"))
						|| File.Exists(Path.Combine(gitDirectory, "app", "src", "main", "AndroidManifest.xml"));
					if (isAndroid)
					{
						type |= ProjectType.Android;
					}
					else
					{
						type |= ProjectType.Gradle;
					}
				}
				// Python
				if (File.Exists(Path.Combine(gitDirectory, "requirements.txt"))
					|| File.Exists(Path.Combine(gitDirectory, "pyproject.toml"))
					|| File.Exists(Path.Combine(gitDirectory, "setup.py")))
				{
					type |= ProjectType.Python;
				}
				// Go
				if (File.Exists(Path.Combine(gitDirectory, "go.mod")))
				{
					type |= ProjectType.Go;
				}
				// .NET
				if (Directory.GetFiles(gitDirectory, "*.sln", SearchOption.TopDirectoryOnly).Length > 0
					|| Directory.GetFiles(gitDirectory, "*.csproj", SearchOption.TopDirectoryOnly).Length > 0)
				{
					type |= ProjectType.DotNet;
				}
				// PHP
				if (File.Exists(Path.Combine(gitDirectory, "composer.json")))
				{
					type |= ProjectType.Php;
				}
			}
			catch (Exception ex)
			{
				Log.Warn("ProjectTypeDetector failed: " + ex.Message);
			}
			return type;
		}
	}
}
