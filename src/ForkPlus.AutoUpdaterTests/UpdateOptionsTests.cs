using System;
using ForkPlus.AutoUpdater;
using Xunit;

namespace ForkPlus.AutoUpdaterTests
{
	/// <summary>
	/// AutoUpdater 命令行参数解析测试：必填项（--url/--install-dir）、键值合法性、
	/// 顺序无关、未知键拒绝、PipeName 与主程序 NamedPipeHelper 同构。
	/// </summary>
	public class UpdateOptionsTests
	{
		private static string[] FullArgs()
		{
			return new string[10]
			{
				"--url", "https://example.com/ForkPlus-4.1.0-windows-x64.zip",
				"--install-dir", "/opt/forkplus",
				"--restart-command", "/opt/forkplus/ForkPlus",
				"--pipe-pid", "1234",
				"--wait-pid", "5678"
			};
		}

		[Fact]
		public void Parse_FullValidArgs_AllFieldsPopulated()
		{
			Program.UpdateOptions options = Program.UpdateOptions.Parse(FullArgs());
			Assert.NotNull(options);
			Assert.Equal("https://example.com/ForkPlus-4.1.0-windows-x64.zip", options.Url);
			Assert.Equal("/opt/forkplus", options.InstallDir);
			Assert.Equal("/opt/forkplus/ForkPlus", options.RestartCommand);
			Assert.Equal(1234, options.PipePid);
			Assert.Equal(5678, options.WaitPid);
			Assert.False(options.NoRestart);
			Assert.Equal(UpdateInstaller.WaitExitTimeoutMs, options.WaitTimeoutMs);
		}

		[Fact]
		public void Parse_ArgumentOrder_DoesNotMatter()
		{
			string[] reordered = new string[9]
			{
				"--wait-pid", "5678",
				"--install-dir", "/opt/forkplus",
				"--url", "https://example.com/a.zip",
				"--pipe-pid", "1234",
				"--no-restart"
			};
			Program.UpdateOptions options = Program.UpdateOptions.Parse(reordered);
			Assert.NotNull(options);
			Assert.Equal("https://example.com/a.zip", options.Url);
			Assert.Equal("/opt/forkplus", options.InstallDir);
			Assert.True(options.NoRestart);
		}

		[Theory]
		[InlineData("ftp://example.com/a.zip")]  // 非 http(s)
		[InlineData("file:///a.zip")]             // 本地路径
		[InlineData("")]                          // 空
		public void Parse_InvalidUrl_ReturnsNull(string url)
		{
			string[] args = new string[4] { "--url", url, "--install-dir", "/opt/forkplus" };
			Assert.Null(Program.UpdateOptions.Parse(args));
		}

		[Fact]
		public void Parse_MissingInstallDir_ReturnsNull()
		{
			string[] args = new string[2] { "--url", "https://example.com/a.zip" };
			Assert.Null(Program.UpdateOptions.Parse(args));
		}

		[Fact]
		public void Parse_MissingUrl_ReturnsNull()
		{
			string[] args = new string[2] { "--install-dir", "/opt/forkplus" };
			Assert.Null(Program.UpdateOptions.Parse(args));
		}

		[Theory]
		[InlineData("--pipe-pid", "0")]
		[InlineData("--pipe-pid", "-1")]
		[InlineData("--pipe-pid", "abc")]
		[InlineData("--wait-pid", "0")]
		[InlineData("--wait-pid", "xyz")]
		[InlineData("--wait-timeout-ms", "-1")]
		public void Parse_InvalidNumericValue_ReturnsNull(string key, string value)
		{
			string[] args = new string[6] { "--url", "https://example.com/a.zip", "--install-dir", "/opt/forkplus", key, value };
			Assert.Null(Program.UpdateOptions.Parse(args));
		}

		[Fact]
		public void Parse_UnknownKey_ReturnsNull()
		{
			string[] args = new string[6] { "--url", "https://example.com/a.zip", "--install-dir", "/opt/forkplus", "--evil", "1" };
			Assert.Null(Program.UpdateOptions.Parse(args));
		}

		[Fact]
		public void Parse_TrailingKeyWithoutValue_DoesNotThrow()
		{
			// --restart-command 落在末尾无值：按 null 值 → 解析失败返回 null
			string[] args = new string[5] { "--url", "https://example.com/a.zip", "--install-dir", "/opt/forkplus", "--restart-command" };
			Assert.Null(Program.UpdateOptions.Parse(args));
		}

		[Fact]
		public void Parse_EmptyArgs_ReturnsNull()
		{
			Assert.Null(Program.UpdateOptions.Parse(new string[0]));
		}

		[Fact]
		public void Parse_CustomWaitTimeout_IsApplied()
		{
			string[] args = new string[6]
			{
				"--url", "https://example.com/a.zip",
				"--install-dir", "/opt/forkplus",
				"--wait-timeout-ms", "1500"
			};
			Program.UpdateOptions options = Program.UpdateOptions.Parse(args);
			Assert.NotNull(options);
			Assert.Equal(1500, options.WaitTimeoutMs);
		}

		/// <summary>管道名与主程序 NamedPipeHelper.CreatePipeName("Update", pid) 同构。</summary>
		[Fact]
		public void PipeName_MatchesMainAppConvention()
		{
			Program.UpdateOptions options = Program.UpdateOptions.Parse(new string[6]
			{
				"--url", "https://example.com/a.zip",
				"--install-dir", "/opt/forkplus",
				"--pipe-pid", "4242"
			});
			Assert.NotNull(options);
			Assert.Equal("Fork_Pipe4242_Update", options.PipeName);
		}
	}
}
