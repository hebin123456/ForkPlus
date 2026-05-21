using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ForkPlus.Tests
{
	public class ClassCoverageManifestTests
	{
		private static readonly Regex TypeDeclarationRegex = new Regex(@"^\s*(?:public|internal|private|protected)?\s*(?:(?:sealed|static|abstract|partial|readonly)\s+)*\s*(class|struct|interface|enum)\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);

		private static readonly string[] ProductionRoots =
		{
			"src/ForkPlus",
			"src/ForkPlus.AskPass",
			"src/ForkPlus.RI"
		};

		[Fact]
		public void EveryProductionType_IsRegisteredInClassCoverageManifest()
		{
			ClassCoverageEntry[] sourceTypes = ReadProductionTypes().ToArray();
			string[] missing = sourceTypes
				.Where((ClassCoverageEntry type) => !ClassCoverageManifest.Entries.Any((ClassCoverageEntry entry) => SameType(entry, type)))
				.Select(Format)
				.ToArray();
			string[] stale = ClassCoverageManifest.Entries
				.Where((ClassCoverageEntry entry) => !sourceTypes.Any((ClassCoverageEntry type) => SameType(entry, type)))
				.Select(Format)
				.ToArray();

			Assert.True(missing.Length == 0, "Missing class coverage entries:\n" + string.Join("\n", missing));
			Assert.True(stale.Length == 0, "Stale class coverage entries:\n" + string.Join("\n", stale));
		}

		[Fact]
		public void EveryProductionType_HasAtLeastOneAutomatedCase()
		{
			string[] missing = ClassCoverageManifest.Entries
				.Where((ClassCoverageEntry entry) => entry.AutomatedCases == null || entry.AutomatedCases.Length == 0)
				.Select(Format)
				.ToArray();

			Assert.True(missing.Length == 0, "Types without automated cases:\n" + string.Join("\n", missing));
		}

		[Fact]
		public void ClassCoverageEntries_ReferenceRegisteredSourceFiles()
		{
			string[] missingFiles = ClassCoverageManifest.Entries
				.Select((ClassCoverageEntry entry) => entry.SourceFile)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.Where((string sourceFile) => !SourceFileCoverageManifest.Files.Contains(sourceFile))
				.ToArray();

			Assert.True(missingFiles.Length == 0, "Class coverage entries reference missing source files:\n" + string.Join("\n", missingFiles));
		}

		[Theory]
		[MemberData(nameof(ProductionTypes))]
		public void ProductionType_HasCoverageEntry(string sourceFile, string typeKind, string typeName)
		{
			Assert.Contains(ClassCoverageManifest.Entries, (ClassCoverageEntry entry) =>
				string.Equals(entry.SourceFile, sourceFile, StringComparison.OrdinalIgnoreCase)
				&& entry.TypeKind == typeKind
				&& entry.TypeName == typeName);
		}

		public static IEnumerable<object[]> ProductionTypes()
		{
			foreach (ClassCoverageEntry entry in ClassCoverageManifest.Entries.OrderBy((ClassCoverageEntry entry) => entry.SourceFile, StringComparer.OrdinalIgnoreCase).ThenBy((ClassCoverageEntry entry) => entry.TypeName))
			{
				yield return new object[] { entry.SourceFile, entry.TypeKind, entry.TypeName };
			}
		}

		private static IEnumerable<ClassCoverageEntry> ReadProductionTypes()
		{
			string repositoryRoot = FindRepositoryRoot();
			foreach (string root in ProductionRoots)
			{
				foreach (string file in Directory.GetFiles(Path.Combine(repositoryRoot, root), "*.cs", SearchOption.AllDirectories))
				{
					string sourceFile = NormalizeRelativePath(repositoryRoot, file);
					if (IsGeneratedPath(sourceFile))
					{
						continue;
					}
					foreach (string line in File.ReadAllLines(file))
					{
						Match match = TypeDeclarationRegex.Match(line);
						if (match.Success)
						{
							yield return new ClassCoverageEntry(sourceFile, match.Groups[1].Value, match.Groups[2].Value, "UNIT-SOURCE-COVERAGE-001");
						}
					}
				}
			}
		}

		private static bool SameType(ClassCoverageEntry lhs, ClassCoverageEntry rhs)
		{
			return string.Equals(lhs.SourceFile, rhs.SourceFile, StringComparison.OrdinalIgnoreCase)
				&& lhs.TypeKind == rhs.TypeKind
				&& lhs.TypeName == rhs.TypeName;
		}

		private static string Format(ClassCoverageEntry entry)
		{
			return entry.SourceFile + " :: " + entry.TypeKind + " " + entry.TypeName;
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
			string directory = AppDomain.CurrentDomain.BaseDirectory;
			while (!string.IsNullOrWhiteSpace(directory))
			{
				if (File.Exists(Path.Combine(directory, "ForkPlus.sln")))
				{
					return directory;
				}
				directory = Path.GetDirectoryName(directory);
			}
			throw new DirectoryNotFoundException("Could not find repository root.");
		}
	}
}
