using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace ForkPlus.Tests
{
	public class AssemblySmokeTests
	{
		[Fact]
		public void ForkPlusAssembly_PublicTypesCanBeEnumerated()
		{
			Type[] publicTypes = typeof(App).Assembly.GetExportedTypes();

			Assert.Contains(publicTypes, (Type type) => type.FullName == "ForkPlus.App");
			Assert.DoesNotContain(publicTypes, (Type type) => type.FullName != null && type.FullName.StartsWith("Fork.", StringComparison.Ordinal));
		}

		[Fact]
		public void HelperAssemblies_CanBeLoadedWhenBuilt()
		{
			string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
			AssertLoadableIfPresent(Path.Combine(baseDirectory, "ForkPlus.AskPass.exe"));
			AssertLoadableIfPresent(Path.Combine(baseDirectory, "ForkPlus.RI.exe"));
		}

		[Fact]
		public void PublicEnums_HaveAtLeastOneValue()
		{
			Type[] emptyEnums = typeof(App).Assembly
				.GetExportedTypes()
				.Where((Type type) => type.IsEnum)
				.Where((Type type) => Enum.GetNames(type).Length == 0)
				.ToArray();

			Assert.True(emptyEnums.Length == 0, "Empty public enums: " + string.Join(", ", emptyEnums.Select((Type type) => type.FullName)));
		}

		private static void AssertLoadableIfPresent(string assemblyPath)
		{
			if (!File.Exists(assemblyPath))
			{
				return;
			}
			Assembly assembly = Assembly.LoadFrom(assemblyPath);
			Assert.NotNull(assembly);
			Assert.NotEmpty(assembly.GetTypes());
		}
	}
}
