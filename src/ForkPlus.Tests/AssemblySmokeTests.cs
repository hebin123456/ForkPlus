using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
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
			// .NET 10 推荐 AppContext.BaseDirectory 替代 AppDomain.CurrentDomain.BaseDirectory
			// （后者仍支持，但前者更轻量、无 AppDomain 语义负担）。
			string baseDirectory = AppContext.BaseDirectory;
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
			// .NET 10 默认 AssemblyLoadContext 行为与 .NET Framework 的 AppDomain 不同：
			// LoadFromAssemblyPath 是推荐的加载入口（不会被标记为 load-from 上下文的"黑洞"）。
			Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
			Assert.NotNull(assembly);
			Assert.NotEmpty(assembly.GetTypes());
		}
	}
}
