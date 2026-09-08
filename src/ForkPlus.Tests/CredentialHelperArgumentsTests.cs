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
	}
}
