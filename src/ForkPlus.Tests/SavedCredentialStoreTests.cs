// 凭据记忆（Layer D）专项测试：SavedCredentialStore 的 prompt 解析、CRUD 语义、
// 落盘重载，以及 ShowAskPassWindowCommand 的静默路径（不再询问快速失败 /
// 已记住密码自动回填——两条路径都在弹窗之前 return，无 UI 依赖可直测）。
// 设计见 docs/credential-popup-unification.md（Layer D 一节）。
using System;
using System.IO;
using ForkPlus.Git;
using ForkPlus.UI.Commands;
using Xunit;

namespace ForkPlus.Tests
{
	public class SavedCredentialStoreTests : IDisposable
	{
		private readonly string _tempFile;

		private readonly SavedCredentialStore _store;

		public SavedCredentialStoreTests()
		{
			_tempFile = Path.Combine(Path.GetTempPath(), "fp-creds-" + Guid.NewGuid().ToString("N") + ".json");
			_store = new SavedCredentialStore(_tempFile);
		}

		public void Dispose()
		{
			try
			{
				File.Delete(_tempFile);
			}
			catch
			{
			}
		}

		// ============================ prompt 解析 ============================

		[Theory]
		[InlineData("Username for 'https://example.com':", "example.com", null)]
		[InlineData("Username for 'https://git.corp.local:8443':", "git.corp.local", null)]
		[InlineData("Password for 'https://octocat@example.com':", "example.com", "octocat")]
		[InlineData("Password for 'http://plain.example.com':", "plain.example.com", "")]
		public void Parse_HttpsPrompts_ExtractHostAndUsername(string prompt, string expectedHost, string expectedUsername)
		{
			if (expectedUsername == null)
			{
				Assert.True(SavedCredentialStore.TryParseUsernamePrompt(prompt, out string host), "应识别为 Username 询问");
				Assert.Equal(expectedHost, host);
				Assert.False(SavedCredentialStore.TryParsePasswordPrompt(prompt, out _, out _), "Username prompt 不应误判为 Password");
			}
			else
			{
				Assert.True(SavedCredentialStore.TryParsePasswordPrompt(prompt, out string host, out string username), "应识别为 Password 询问");
				Assert.Equal(expectedHost, host);
				Assert.Equal(expectedUsername, username ?? "");
				Assert.False(SavedCredentialStore.TryParseUsernamePrompt(prompt, out _), "Password prompt 不应误判为 Username");
			}
		}

		[Theory]
		[InlineData("Username for 'ssh://git@example.com':")]
		[InlineData("Password for 'ssh://example.com':")]
		[InlineData("Enter passphrase for key '/home/user/.ssh/id_ed25519':")]
		[InlineData("git@github.com's password:")]
		[InlineData("")]
		public void Parse_NonHttpsPrompts_Rejected(string prompt)
		{
			Assert.False(SavedCredentialStore.TryParseUsernamePrompt(prompt, out _));
			Assert.False(SavedCredentialStore.TryParsePasswordPrompt(prompt, out _, out _));
		}

		// ============================ CRUD 语义 ============================

		[Fact]
		public void RememberUsername_StoresUsernameOnly_NoSilentHit()
		{
			_store.RememberUsername("example.com", "octocat");

			SavedCredentialStore.SavedCredential entry = _store.FindEntry("example.com");
			Assert.NotNull(entry);
			Assert.Equal("octocat", entry.Username);
			Assert.False(entry.HasPassword, "只记账号不应命中密码查询");
			Assert.False(_store.TryGetSilentCredential("example.com", out _, out _), "只记账号（第一档）不应静默回填");
		}

		[Fact]
		public void RememberPassword_StoresButNotSilent_WithoutNeverAsk()
		{
			_store.RememberPassword("example.com", "octocat", "secret123");

			SavedCredentialStore.SavedCredential entry = _store.FindEntry("example.com");
			Assert.NotNull(entry);
			Assert.Equal("octocat", entry.Username);
			Assert.Equal("secret123", entry.Password);
			Assert.True(entry.HasPassword);
			// 第二档（记住密码、未开不再弹出）：不静默——下次仍弹窗，由弹窗预填密码
			Assert.False(_store.TryGetSilentCredential("example.com", out _, out _), "记住密码未开不再弹出（第二档）不应静默回填");
		}

		[Fact]
		public void SilentCredential_OnlyHitsWhenPasswordAndNeverAsk()
		{
			// 三档矩阵：仅 password + NeverAskAgain 皆备（第三档）才静默命中
			_store.RememberPassword("example.com", "octocat", "secret123");
			Assert.False(_store.TryGetSilentCredential("example.com", out _, out _));

			_store.SetNeverAsk("example.com", enabled: true);
			Assert.True(_store.TryGetSilentCredential("example.com", out string username, out string password));
			Assert.Equal("octocat", username);
			Assert.Equal("secret123", password);

			// 凭据失效（erase 联动清密码）后不命中，但标记保留（快速失败由 Command 层处理）
			_store.ForgetPassword("example.com");
			Assert.False(_store.TryGetSilentCredential("example.com", out _, out _));
			Assert.True(_store.FindEntry("example.com").NeverAskAgain);
		}

		[Fact]
		public void Upsert_OverwritesEntry_AndRemovesBareEntry()
		{
			_store.RememberPassword("example.com", "octocat", "secret123");

			// 偏好页编辑：改账号/密码 + 开"不再弹出"，一次写入
			_store.Upsert("example.com", "newcat", "newsecret", neverAsk: true);
			SavedCredentialStore.SavedCredential entry = _store.FindEntry("example.com");
			Assert.Equal("newcat", entry.Username);
			Assert.Equal("newsecret", entry.Password);
			Assert.True(entry.NeverAskAgain);
			Assert.True(_store.TryGetSilentCredential("example.com", out _, out _));

			// 三样皆空 → 条目删除（避免垃圾条目堆积）
			_store.Upsert("example.com", "", "", neverAsk: false);
			Assert.Null(_store.FindEntry("example.com"));
		}

		[Fact]
		public void RememberUsername_KeepsExistingPasswordAndNeverAsk()
		{
			_store.RememberPassword("example.com", "octocat", "secret123");
			_store.SetNeverAsk("example.com", enabled: true);

			_store.RememberUsername("example.com", "newcat");

			SavedCredentialStore.SavedCredential entry = _store.FindEntry("example.com");
			Assert.Equal("newcat", entry.Username);
			Assert.True(entry.HasPassword, "改账号不应丢已记住的密码");
			Assert.True(entry.NeverAskAgain, "改账号不应丢不再询问标记");
		}

		[Fact]
		public void ForgetPassword_ClearsPassword_KeepsUsernameAndNeverAsk()
		{
			_store.RememberPassword("example.com", "octocat", "secret123");
			_store.SetNeverAsk("example.com", enabled: true);

			_store.ForgetPassword("example.com");

			SavedCredentialStore.SavedCredential entry = _store.FindEntry("example.com");
			Assert.NotNull(entry);
			Assert.Equal("octocat", entry.Username);
			Assert.False(entry.HasPassword, "失效密码应清除");
			Assert.True(entry.NeverAskAgain);
		}

		[Fact]
		public void ForgetPassword_BareEntryIsRemoved()
		{
			// bare 条目（无密码无账号、neverAsk 关闭，如 SetNeverAsk(true→false) 后的残留形态）
			// 再清密码时顺手删除，避免垃圾条目堆积
			_store.SetNeverAsk("example.com", enabled: true);
			_store.SetNeverAsk("example.com", enabled: false);
			Assert.NotNull(_store.FindEntry("example.com"));

			_store.ForgetPassword("example.com");
			Assert.Null(_store.FindEntry("example.com"));
		}

		[Fact]
		public void SetNeverAsk_And_ClearAllNeverAsk()
		{
			_store.SetNeverAsk("a.example.com", enabled: true);
			_store.SetNeverAsk("b.example.com", enabled: true);
			_store.RememberUsername("c.example.com", "cat");

			_store.ClearAllNeverAsk();

			Assert.False(_store.FindEntry("a.example.com").NeverAskAgain);
			Assert.False(_store.FindEntry("b.example.com").NeverAskAgain);
			Assert.NotNull(_store.FindEntry("c.example.com"));
		}

		[Fact]
		public void Remove_DeletesWholeEntry()
		{
			_store.RememberPassword("example.com", "octocat", "secret123");
			_store.Remove("example.com");
			Assert.Null(_store.FindEntry("example.com"));
		}

		[Fact]
		public void GetAll_ReturnsHostSortedSnapshot()
		{
			_store.RememberUsername("z.example.com", "z");
			_store.RememberUsername("a.example.com", "a");

			var all = _store.GetAll();
			Assert.Equal(2, all.Count);
			Assert.Equal("a.example.com", all[0].Host);
			Assert.Equal("z.example.com", all[1].Host);
		}

		// ============================ 落盘重载 ============================

		[Fact]
		public void Store_PersistsAndReloads()
		{
			_store.RememberPassword("example.com", "octocat", "secret123");
			_store.SetNeverAsk("example.com", enabled: true);
			_store.RememberUsername("other.com", "someone");

			var reloaded = new SavedCredentialStore(_tempFile);
			Assert.True(reloaded.TryGetSilentCredential("example.com", out string username, out string password));
			Assert.Equal("octocat", username);
			Assert.Equal("secret123", password);
			Assert.True(reloaded.FindEntry("example.com").NeverAskAgain);
			Assert.Equal("someone", reloaded.FindEntry("other.com").Username);
			Assert.Equal(2, reloaded.GetAll().Count);
		}

		[Fact]
		public void Load_MalformedFile_StartsEmpty()
		{
			File.WriteAllText(_tempFile, "{ not valid json !!!");
			var store = new SavedCredentialStore(_tempFile);
			Assert.Empty(store.GetAll());
		}

		// ============================ ShowAskPassWindowCommand 静默路径 ============================

		[Fact]
		public void Command_NeverAskHost_ReturnsEmptyWithoutPrompting()
		{
			SavedCredentialStore previous = SavedCredentialStore.SwapForTests(_store);
			try
			{
				_store.SetNeverAsk("example.com", enabled: true);
				var command = new ShowAskPassWindowCommand();

				command.Execute("Username for 'https://example.com':", noPrompt: false, "", out string result);
				Assert.Equal("", result);

				command.Execute("Password for 'https://example.com':", noPrompt: false, "", out string result2);
				Assert.Equal("", result2);
			}
			finally
			{
				SavedCredentialStore.SwapForTests(previous);
			}
		}

		[Fact]
		public void Command_RememberedPasswordWithoutNeverAsk_DoesNotSilentlyFill()
		{
			SavedCredentialStore previous = SavedCredentialStore.SwapForTests(_store);
			try
			{
				// 第二档（记住密码、未开不再弹出）：不静默——下次弹窗仍出现（密码框预填）。
				// noPrompt=true 时走到弹窗判断返回空，证明静默路径未命中（误吞则同样返回空
				// 无法区分，故正向断言见第三档用例，此处守住"第二档绝不静默"的回归线）。
				_store.RememberPassword("example.com", "octocat", "secret123");
				var command = new ShowAskPassWindowCommand();

				command.Execute("Password for 'https://octocat@example.com':", noPrompt: true, "", out string result);
				Assert.Equal("", result);
			}
			finally
			{
				SavedCredentialStore.SwapForTests(previous);
			}
		}

		[Fact]
		public void Command_SilentCredential_WhenRememberedPasswordAndNeverAsk()
		{
			SavedCredentialStore previous = SavedCredentialStore.SwapForTests(_store);
			try
			{
				// 第三档（记住密码 + 不再弹出）：askpass 兜底路径静默回填（主路径在 credential get）
				_store.RememberPassword("example.com", "octocat", "secret123");
				_store.SetNeverAsk("example.com", enabled: true);
				var command = new ShowAskPassWindowCommand();

				command.Execute("Password for 'https://octocat@example.com':", noPrompt: false, "", out string result);
				Assert.Equal("secret123", result);

				command.Execute("Username for 'https://example.com':", noPrompt: false, "", out string result2);
				Assert.Equal("octocat", result2);
			}
			finally
			{
				SavedCredentialStore.SwapForTests(previous);
			}
		}

		[Fact]
		public void Command_UsernameOnlyHost_DoesNotSilentlyFill()
		{
			SavedCredentialStore previous = SavedCredentialStore.SwapForTests(_store);
			try
			{
				_store.RememberUsername("example.com", "octocat");
				var command = new ShowAskPassWindowCommand();

				// 只记账号：username 询问仍应弹窗（预填交互），不能静默返回——
				// 这里用 noPrompt=true 断言"走到了弹窗判断"（若误吞则返回空，与预期无法区分，
				// 改判 noPrompt=false 会真实弹窗阻塞，故断言点选 noPrompt 分支的可达性：
				// 只记账号 + noPrompt → 空（没有静默凭据可用））
				command.Execute("Username for 'https://example.com':", noPrompt: true, "", out string result);
				Assert.Equal("", result);
			}
			finally
			{
				SavedCredentialStore.SwapForTests(previous);
			}
		}
	}
}
