using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace ForkPlus.Tests
{
	public class SourceFileCoverageManifestTests
	{
		private static readonly string[] ProductionRoots =
		{
			"src/ForkPlus",
			"src/ForkPlus.AskPass",
			"src/ForkPlus.RI"
		};

		[Fact]
		public void EveryProductionSourceFile_IsRegisteredInCoverageManifest()
		{
			string repositoryRoot = FindRepositoryRoot();
			string[] sourceFiles = ProductionRoots
				.SelectMany((string root) => Directory.GetFiles(Path.Combine(repositoryRoot, root), "*.cs", SearchOption.AllDirectories))
				.Select((string path) => NormalizeRelativePath(repositoryRoot, path))
				.Where((string path) => !IsGeneratedPath(path))
				.OrderBy((string path) => path, StringComparer.OrdinalIgnoreCase)
				.ToArray();

			string[] missing = sourceFiles
				.Where((string path) => !SourceFileCoverageManifest.Files.Contains(path))
				.ToArray();
			string[] stale = SourceFileCoverageManifest.Files
				.Where((string path) => !sourceFiles.Contains(path))
				.ToArray();

			Assert.True(missing.Length == 0, "Missing source file coverage entries:\n" + string.Join("\n", missing));
			Assert.True(stale.Length == 0, "Stale source file coverage entries:\n" + string.Join("\n", stale));
		}

		[Theory]
		[MemberData(nameof(ProductionSourceFiles))]
		public void ProductionSourceFile_HasCoverageEntry(string sourceFile)
		{
			Assert.Contains(sourceFile, SourceFileCoverageManifest.Files);
		}

		public static IEnumerable<object[]> ProductionSourceFiles()
		{
			foreach (string file in SourceFileCoverageManifest.Files.OrderBy((string path) => path, StringComparer.OrdinalIgnoreCase))
			{
				yield return new object[] { file };
			}
		}

		private static bool IsGeneratedPath(string path)
		{
			return path.Contains("/bin/") || path.Contains("/obj/") || path.Contains("/obj_agent/");
		}

		private static string NormalizeRelativePath(string repositoryRoot, string path)
		{
			return path.Substring(repositoryRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace('\\', '/');
		}

		private static string FindRepositoryRoot()
		{
			// 迁移适配：原版向上找根级 ForkPlus.sln（原仓库 sln 在根目录）。
			// 向上遍历可能在 src/ 等中间目录提前命中 sln → 返回错误根，
			// 拼出 .../src/src/ForkPlus 双重路径（DirectoryNotFoundException）。
			// 改为找 .git（git 仓根，目录或 worktree 文件均可），任何仓库布局都成立。
			string directory = AppContext.BaseDirectory;
			while (!string.IsNullOrWhiteSpace(directory))
			{
				if (Directory.Exists(Path.Combine(directory, ".git")) || File.Exists(Path.Combine(directory, ".git")))
				{
					return directory;
				}
				directory = Path.GetDirectoryName(directory);
			}
			throw new DirectoryNotFoundException("Could not find repository root.");
		}
	}
}
