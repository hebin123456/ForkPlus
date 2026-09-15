using ForkPlus.Git;
using Xunit;

namespace ForkPlus.Tests
{
	/// <summary>
	/// 凭据收编（Layer C）回归测试：凭据描述的 password 行解析。
	/// 见 docs/credential-popup-unification.md。
	///
	/// git 凭据协议：helper 的 get 输入只含 protocol/host/username，
	/// store/erase 输入还含 password。收编前 password 行落 default 分支
	/// （打"Unknown credentials description parameter"警告），store 语义拿不到密码。
	/// </summary>
	public class CredentialHelperArgumentsTests
	{
		[Fact]
		public void Parse_StoreInputWithPassword_CapturesPassword()
		{
			CredentialHelperArguments arguments = CredentialHelperArguments.Parse(
				"protocol=https\nhost=github.com\nusername=alice\npassword=ghp_secret\n");

			Assert.NotNull(arguments);
			Assert.Equal("https", arguments.Protocol);
			Assert.Equal("github.com", arguments.Host);
			Assert.Equal("alice", arguments.Username);
			Assert.Equal("ghp_secret", arguments.Password);
		}

		[Fact]
		public void Parse_GetInputWithoutPassword_LeavesPasswordNull()
		{
			CredentialHelperArguments arguments = CredentialHelperArguments.Parse(
				"protocol=https\nhost=github.com\nusername=alice\n");

			Assert.NotNull(arguments);
			Assert.Equal("alice", arguments.Username);
			Assert.Null(arguments.Password);
		}

		[Fact]
		public void Parse_InputWithoutUsername_LeavesUsernameNull()
		{
			CredentialHelperArguments arguments = CredentialHelperArguments.Parse(
				"protocol=https\nhost=github.com\npassword=secret\n");

			Assert.NotNull(arguments);
			Assert.Null(arguments.Username);
			Assert.Equal("secret", arguments.Password);
		}

		[Fact]
		public void Parse_PasswordContainingEquals_IsPreservedVerbatim()
		{
			// 值内含 '='：取第一个 '=' 作键值分隔，密码原样保留。
			CredentialHelperArguments arguments = CredentialHelperArguments.Parse(
				"protocol=https\nhost=example.com\npassword=pa==ss\n");

			Assert.NotNull(arguments);
			Assert.Equal("pa==ss", arguments.Password);
		}

		[Fact]
		public void Parse_UnknownParameter_DoesNotBreakKnownFields()
		{
			// git 后续版本可能追加新字段（如 path/wwwauth[]），未知行只警告不致命。
			CredentialHelperArguments arguments = CredentialHelperArguments.Parse(
				"protocol=https\nhost=github.com\npath=org/repo.git\nusername=alice\n");

			Assert.NotNull(arguments);
			Assert.Equal("github.com", arguments.Host);
			Assert.Equal("alice", arguments.Username);
		}

		[Fact]
		public void Export_RoundTripsPassword()
		{
			string exported = new CredentialHelperArguments("github.com", "https", "alice")
			{
				Password = "secret"
			}.Export();

			Assert.Contains("protocol=https\n", exported);
			Assert.Contains("host=github.com\n", exported);
			Assert.Contains("username=alice\n", exported);
			Assert.Contains("password=secret\n", exported);
		}

		[Fact]
		public void Export_WithoutPassword_OmitsPasswordLine()
		{
			string exported = new CredentialHelperArguments("github.com", "https", "alice").Export();

			Assert.DoesNotContain("password=", exported);
		}

		[Fact]
		public void Parse_MissingProtocol_ReturnsNull()
		{
			Assert.Null(CredentialHelperArguments.Parse("host=github.com\n"));
		}

		[Fact]
		public void Parse_MissingHost_ReturnsNull()
		{
			Assert.Null(CredentialHelperArguments.Parse("protocol=https\n"));
		}

		[Fact]
		public void ParseAndExport_RoundTripThroughStoreInput()
		{
			// store 链路的完整契约：git 写入的凭据经 Parse/Export 原样回到协议格式。
			string input = "protocol=https\nhost=github.com\nusername=alice\npassword=ghp_token\n";
			CredentialHelperArguments arguments = CredentialHelperArguments.Parse(input);

			Assert.NotNull(arguments);
			string exported = arguments.Export();
			Assert.Contains("username=alice\n", exported);
			Assert.Contains("password=ghp_token\n", exported);
		}

		// 修复（2026-09-14，"git mm init 弹凭据窗：明明凭据管理器里有凭据还是反复询问"）：
		// credential.usehttppath=true（git-mm workspace 用户全局启用）时 git 的 get/store/erase
		// 描述带 path= 行——此前被当未知参数丢弃，GcmCompatibleStore 只能查 host 级键，
		// 而 GCM 该配置下按完整路径存凭据 → 永远查不到。
		[Fact]
		public void Parse_InputWithPath_CapturesPath()
		{
			CredentialHelperArguments arguments = CredentialHelperArguments.Parse(
				"protocol=https\nhost=codehub-git-codeartsx.rnd.yinwang.com\npath=innersource/T4VB_G/TNC/build/manifest.git\n");

			Assert.NotNull(arguments);
			Assert.Equal("innersource/T4VB_G/TNC/build/manifest.git", arguments.Path);
		}

		[Fact]
		public void Parse_StoreInputWithPathAndCredential_CapturesAll()
		{
			// git 认证成功后 store 的完整输入：path 与 username/password 并存。
			CredentialHelperArguments arguments = CredentialHelperArguments.Parse(
				"protocol=https\nhost=codehub-git-codeartsx.rnd.yinwang.com\npath=innersource/T4VB_G/TNC/build/manifest.git\nusername=h00003968\npassword=secret\n");

			Assert.NotNull(arguments);
			Assert.Equal("innersource/T4VB_G/TNC/build/manifest.git", arguments.Path);
			Assert.Equal("h00003968", arguments.Username);
			Assert.Equal("secret", arguments.Password);
		}

		[Fact]
		public void Parse_InputWithoutPath_LeavesPathNull()
		{
			CredentialHelperArguments arguments = CredentialHelperArguments.Parse(
				"protocol=https\nhost=github.com\nusername=alice\n");

			Assert.NotNull(arguments);
			Assert.Null(arguments.Path);
		}

		[Fact]
		public void Export_DoesNotEchoPath()
		{
			// get 响应只需回填 username/password（git 自己持有 path 上下文），
			// 回显 path 行无契约保证——保持 Export 不变。
			string exported = new CredentialHelperArguments("github.com", "https", "alice", "org/repo.git")
			{
				Password = "secret"
			}.Export();

			Assert.DoesNotContain("path=", exported);
			Assert.Contains("username=alice\n", exported);
			Assert.Contains("password=secret\n", exported);
		}
	}
}
