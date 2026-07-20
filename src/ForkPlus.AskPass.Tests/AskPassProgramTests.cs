using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Xunit;

namespace ForkPlus.AskPass.Tests
{
	/// <summary>
	/// ForkPlus.AskPass.exe 单元测试。
	///
	/// 测试策略：
	/// 1. 黑盒进程测试：启动 exe，验证 exit code（隔离环境变量，无副作用）
	/// 2. 反射测试：通过 AssemblyLoadContext.Default.LoadFromAssemblyPath 加载托管 dll，
	///    反射调用 private static 方法（Program 是 internal，但反射可绕过访问限制）
	///
	/// 注意：.NET 10 下 .exe 是 native apphost（启动器），不是托管程序集；
	/// 托管代码在同名的 .dll 中，反射加载用 .dll，进程启动用 .exe。
	///
	/// AskPass.exe 的 Main 逻辑：
	///   - 无 FORK_PLUS_PROCESS_ID → 立即返回 1
	///   - 有 processId → 连接 NamedPipe，超时 30s（测试不覆盖，避免慢测试）
	///   - 参数首项为 get/store/erase → credential helper 模式（从 stdin 读取）
	///   - 其他参数 → 普通模式（args 拼接为 request）
	/// </summary>
	public class AskPassProgramTests
	{
		private static string ExePath =>
			// .NET 10 推荐 AppContext.BaseDirectory 替代 AppDomain.CurrentDomain.BaseDirectory
			Path.Combine(AppContext.BaseDirectory, "ForkPlus.AskPass.exe");

		private static string ManagedDllPath =>
			// .NET 10 下托管代码在 .dll 中（.exe 是 native apphost）
			Path.Combine(AppContext.BaseDirectory, "ForkPlus.AskPass.dll");

		/// <summary>
		/// 加载 ForkPlus.AskPass.dll 托管程序集，反射查找 Program 类型。
		/// .NET 10 上用 AssemblyLoadContext.Default.LoadFromAssemblyPath 替代
		/// Assembly.LoadFrom（后者仍可用但行为略不同，ALC 是推荐方式）。
		/// </summary>
		private static Type LoadProgramType()
		{
			var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(ManagedDllPath);
			return assembly.GetType("ForkPlus.AskPass.Program");
		}

		/// <summary>
		/// 启动 ForkPlus.AskPass.exe，传入参数和环境变量，返回 exit code。
		/// 超时 10 秒（无 processId 时应立即返回，10s 足够）。
		/// </summary>
		private static int RunAskPass(string args, Action<ProcessStartInfo> configureEnv = null)
		{
			var psi = new ProcessStartInfo
			{
				FileName = ExePath,
				Arguments = args ?? "",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			};
			// 关键：清空 FORK_PLUS_PROCESS_ID，避免继承测试进程的环境变量
			psi.EnvironmentVariables["FORK_PLUS_PROCESS_ID"] = "";
			psi.EnvironmentVariables["FORK_PLUS_REPOSITORY_PATH"] = "";
			psi.EnvironmentVariables["NO_PROMPT"] = "";
			configureEnv?.Invoke(psi);

			using (var proc = Process.Start(psi))
			{
				Assert.NotNull(proc);
				// 读取 stdout/stderr 避免缓冲区满导致卡死
				proc.StandardOutput.ReadToEnd();
				proc.StandardError.ReadToEnd();
				if (!proc.WaitForExit(10000))
				{
					proc.Kill();
					proc.WaitForExit(2000);
					throw new TimeoutException("ForkPlus.AskPass.exe 未在 10 秒内退出。");
				}
				return proc.ExitCode;
			}
		}

		[Fact]
		public void Exe_Exists()
		{
			Assert.True(File.Exists(ExePath), $"ForkPlus.AskPass.exe 未找到于 {ExePath}。");
		}

		[Fact]
		public void NoProcessId_Returns1()
		{
			// 无 FORK_PLUS_PROCESS_ID 环境变量 → Main 立即返回 1
			Assert.Equal(1, RunAskPass(""));
		}

		[Fact]
		public void NoProcessId_WithArgs_Returns1()
		{
			// 带参数但无 processId → 仍返回 1（processId 检查在参数处理之前）
			Assert.Equal(1, RunAskPass("Enter passphrase for key '/home/user/.ssh/id_rsa':"));
		}

		[Fact]
		public void NoProcessId_CredentialHelperAction_Returns1()
		{
			// 首参数是 "get"（credential helper action）但无 processId → 返回 1
			Assert.Equal(1, RunAskPass("get"));
		}

		[Fact]
		public void NoPromptMode_NoProcessId_Returns1()
		{
			// 设 NO_PROMPT=1 但无 processId → 仍在 processId 检查处返回 1
			Assert.Equal(1, RunAskPass("", psi =>
			{
				psi.EnvironmentVariables["NO_PROMPT"] = "1";
			}));
		}

		[Fact]
		public void IsCredentialHelperAction_RecognizesGet()
		{
			// 反射测试 private static IsCredentialHelperAction("get") == true
			var method = LoadProgramType()?.GetMethod("IsCredentialHelperAction",
				BindingFlags.Static | BindingFlags.NonPublic);
			Assert.NotNull(method);
			Assert.True((bool)method.Invoke(null, new object[] { "get" }));
		}

		[Fact]
		public void IsCredentialHelperAction_RecognizesStore()
		{
			var method = LoadProgramType()?.GetMethod("IsCredentialHelperAction",
				BindingFlags.Static | BindingFlags.NonPublic);
			Assert.NotNull(method);
			Assert.True((bool)method.Invoke(null, new object[] { "store" }));
		}

		[Fact]
		public void IsCredentialHelperAction_RecognizesErase()
		{
			var method = LoadProgramType()?.GetMethod("IsCredentialHelperAction",
				BindingFlags.Static | BindingFlags.NonPublic);
			Assert.NotNull(method);
			Assert.True((bool)method.Invoke(null, new object[] { "erase" }));
		}

		[Fact]
		public void IsCredentialHelperAction_RecognizesCaseInsensitive()
		{
			// git credential helper 协议的 action 不区分大小写
			var method = LoadProgramType()?.GetMethod("IsCredentialHelperAction",
				BindingFlags.Static | BindingFlags.NonPublic);
			Assert.NotNull(method);
			Assert.True((bool)method.Invoke(null, new object[] { "GET" }));
			Assert.True((bool)method.Invoke(null, new object[] { "Store" }));
			Assert.True((bool)method.Invoke(null, new object[] { "ERASE" }));
		}

		[Fact]
		public void IsCredentialHelperAction_RejectsNonAction()
		{
			// 非 credential helper action（如普通 prompt）→ false
			var method = LoadProgramType()?.GetMethod("IsCredentialHelperAction",
				BindingFlags.Static | BindingFlags.NonPublic);
			Assert.NotNull(method);
			Assert.False((bool)method.Invoke(null, new object[] { "Username for 'https://example.com':" }));
			Assert.False((bool)method.Invoke(null, new object[] { "Enter passphrase" }));
			Assert.False((bool)method.Invoke(null, new object[] { "" }));
		}
	}
}
