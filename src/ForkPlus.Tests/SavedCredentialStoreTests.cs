// 凭据记忆（Layer D）专项测试：SavedCredentialStore 的 prompt 解析、CRUD 语义、
// 落盘重载，以及 ShowAskPassWindowCommand 的静默路径（不再询问快速失败 /
// 已记住密码自动回填——两条路径都在弹窗之前 return，无 UI 依赖可直测）。
// 2026-09-14 起"记住密码"即静默（旧第二/三档并入同一语义），NeverAskAgain 仅保留
// 偏好页开关态与"密码被 erase 后快速失败"标记两语义。
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
			// 会话级单词提示缓存是进程级静态：每用例前清空，避免串扰
			SavedCredentialStore.ClearSessionBareCredentialsForTests();
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
		// 修复（2026-09-14，弹窗适配 git-mm）：regex 加 IgnoreCase，兼容个别工具/旧版
		// git 的小写提示变体
		[InlineData("username for 'https://example.com':", "example.com", null)]
		// 修复（2026-09-15，生产日志实锤的 git-mm 变体）：小写 + URL 不带引号；
		// password 变体 userinfo 为空（https://@host/path）
		[InlineData("username for https://codehub-git-codeartsx.rnd.yinwang.com/innersource/T4VB_G/TNC/build/manifest.git", "codehub-git-codeartsx.rnd.yinwang.com", null)]
		[InlineData("password for https://@codehub-git-codeartsx.rnd.yinwang.com/innersource/T4VB_G/TNC/build/manifest.git", "codehub-git-codeartsx.rnd.yinwang.com", "")]
		[InlineData("password for https://octocat@example.com/path/repo.git", "example.com", "octocat")]
		[InlineData("Password for 'https://octocat@example.com':", "example.com", "octocat")]
		[InlineData("password for 'https://octocat@example.com':", "example.com", "octocat")]
		[InlineData("Password for 'http://plain.example.com':", "plain.example.com", "")]
		// 修复（2026-09-11，用户名窗变成之前输过的密码）：prompt URL 的 userinfo 可能携带
		// "<用户名>:<密码>@" 密文，解析须剥掉 ':' 后的密码部分，只留纯用户名——否则恰会
		// 存成 Username，下次 Username 询问把 `octocat:secret` 预填进用户名框。
		[InlineData("Password for 'https://octocat:topsecret@example.com':", "example.com", "octocat")]
		[InlineData("Password for 'https://:onlysecret@example.com':", "example.com", "")]
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
		public void RememberPassword_IsSilent_ByNewSemantics()
		{
			_store.RememberPassword("example.com", "octocat", "secret123");

			SavedCredentialStore.SavedCredential entry = _store.FindEntry("example.com");
			Assert.NotNull(entry);
			Assert.Equal("octocat", entry.Username);
			Assert.Equal("secret123", entry.Password);
			Assert.True(entry.HasPassword);
			// 修复（2026-09-14，"勾了记住密码还不停询问"）：password 非空即静默回填，
			// 不再要求 NeverAskAgain 双条件（明文本就落盘，弹窗确认无安全增益）
			Assert.True(_store.TryGetSilentCredential("example.com", out string username, out string password), "记住密码（未开不再弹出）应静默回填");
			Assert.Equal("octocat", username);
			Assert.Equal("secret123", password);
		}

		[Fact]
		public void SilentCredential_PasswordAloneSuffices_EraseStopsHit()
		{
			// 修复（2026-09-14）矩阵：password 非空即静默命中（NeverAskAgain 不再参与）；
			// 凭据失效（erase 联动清密码）后不命中，账号记忆与标记保留
			// （快速失败由 Command 层按 NeverAskAgain 处理）
			_store.RememberPassword("example.com", "octocat", "secret123");
			Assert.True(_store.TryGetSilentCredential("example.com", out _, out _));

			_store.ForgetPassword("example.com");
			Assert.False(_store.TryGetSilentCredential("example.com", out _, out _));
			Assert.NotNull(_store.FindEntry("example.com"));
			Assert.False(_store.FindEntry("example.com").HasPassword);
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
		public void ForgetAllPasswords_ClearsPasswordsAndMarks_KeepsUsernames()
		{
			// 修复（2026-09-14，"全局重开仍静默"）：偏好页 Ask Again for All Hosts 改为
			// 忘掉全部密码+清标记（账号记忆保留）；静默语义并入"记住密码"后仅清标记
			// 不再恢复弹窗。清空后变全空的 bare 条目顺手删除。
			_store.RememberPassword("a.example.com", "alice", "pw-a");
			_store.SetNeverAsk("a.example.com", enabled: true);
			_store.RememberUsername("b.example.com", "bob");
			_store.SetNeverAsk("b.example.com", enabled: true);
			_store.SetNeverAsk("c.example.com", enabled: true);

			_store.ForgetAllPasswords();

			Assert.False(_store.FindEntry("a.example.com").HasPassword, "全局重开应忘掉密码");
			Assert.False(_store.FindEntry("a.example.com").NeverAskAgain);
			Assert.Equal("alice", _store.FindEntry("a.example.com").Username);
			Assert.False(_store.FindEntry("b.example.com").NeverAskAgain);
			Assert.Equal("bob", _store.FindEntry("b.example.com").Username);
			Assert.Null(_store.FindEntry("c.example.com"));
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
		public void Command_RememberedPassword_SilentlyFills_EvenWithoutNeverAsk()
		{
			SavedCredentialStore previous = SavedCredentialStore.SwapForTests(_store);
			try
			{
				// 修复（2026-09-14，"勾了记住密码还不停询问"）：记住密码即静默——
				// noPrompt=true 也在弹窗判断之前回填（旧"第二档"会走到弹窗分支返回空）
				_store.RememberPassword("example.com", "octocat", "secret123");
				var command = new ShowAskPassWindowCommand();

				command.Execute("Password for 'https://octocat@example.com':", noPrompt: true, "", out string result);
				Assert.Equal("secret123", result);
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
				// 记住密码（+不再弹出标记）：askpass 兜底路径静默回填（主路径在 credential get）
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
		public void Command_SilentPasswordWithoutUserInfo_ReturnsPasswordNotUsername()
		{
			SavedCredentialStore previous = SavedCredentialStore.SwapForTests(_store);
			try
			{
				// 修复（2026-09-11，passphrase 识别 Bug）："不再弹出"的 Password 询问其 prompt URL
				// 可能不含 userinfo（promptUsername=null，如 'https://example.com'）。修复前该分支
				// 走 `promptUsername != null || entry.Username != null` 的共用判定，会误把主机账号
				// （octocat）当密码返回——认证必败。修复后严格按"密码询问回密码"分流，与 userinfo 无关。
				_store.RememberPassword("example.com", "octocat", "secret123");
				_store.SetNeverAsk("example.com", enabled: true);
				var command = new ShowAskPassWindowCommand();

				command.Execute("Password for 'https://example.com':", noPrompt: false, "", out string result);
				Assert.Equal("secret123", result);
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

		// ============================ git-mm 单词提示：会话缓存（2026-09-14） ============================

		[Fact]
		public void SessionBareCache_RememberAndTryGet()
		{
			Assert.False(SavedCredentialStore.TryGetSessionBareUsername(out _));
			Assert.False(SavedCredentialStore.TryGetSessionBarePassword(out _));

			SavedCredentialStore.RememberSessionBareUsername("h00003968");
			SavedCredentialStore.RememberSessionBarePassword("secret-token");

			Assert.True(SavedCredentialStore.TryGetSessionBareUsername(out string username));
			Assert.Equal("h00003968", username);
			Assert.True(SavedCredentialStore.TryGetSessionBarePassword(out string password));
			Assert.Equal("secret-token", password);

			// 空值不写入（清空语义防护）
			SavedCredentialStore.ClearSessionBareCredentialsForTests();
			SavedCredentialStore.RememberSessionBareUsername("");
			SavedCredentialStore.RememberSessionBarePassword(null);
			Assert.False(SavedCredentialStore.TryGetSessionBareUsername(out _));
			Assert.False(SavedCredentialStore.TryGetSessionBarePassword(out _));
		}

		[Fact]
		public void Command_BarePrompts_AskOncePerSession_ThenSilent()
		{
			// 修复（2026-09-14，"单词提示反复弹用户名+密码"）：首次无缓存 + noPrompt →
			// 快速失败不弹窗；答过（会话缓存）后同会话静默复用
			SavedCredentialStore previous = SavedCredentialStore.SwapForTests(_store);
			try
			{
				var command = new ShowAskPassWindowCommand();

				command.Execute("username", noPrompt: true, "", out string firstUsername);
				Assert.Equal("", firstUsername);
				command.Execute("password", noPrompt: true, "", out string firstPassword);
				Assert.Equal("", firstPassword);

				SavedCredentialStore.RememberSessionBareUsername("h00003968");
				SavedCredentialStore.RememberSessionBarePassword("secret-token");

				command.Execute("username", noPrompt: false, "", out string cachedUsername);
				Assert.Equal("h00003968", cachedUsername);
				command.Execute("password", noPrompt: false, "", out string cachedPassword);
				Assert.Equal("secret-token", cachedPassword);
				// noPrompt（后台周期）同样吃会话缓存（用户本会话已答过）
				command.Execute("password", noPrompt: true, "", out string cachedPassword2);
				Assert.Equal("secret-token", cachedPassword2);

				// 单词提示不写 host 记忆（无 URL 关联）
				Assert.Empty(_store.GetAll());
			}
			finally
			{
				SavedCredentialStore.SwapForTests(previous);
			}
		}

		[Fact]
		public void Command_StandardPrompts_BridgeFromSessionBareCredentials()
		{
			// 会话桥接：同会话答过单词提示 → 标准格式询问在 host 无记忆时静默复用并落盘
			SavedCredentialStore previous = SavedCredentialStore.SwapForTests(_store);
			try
			{
				SavedCredentialStore.RememberSessionBareUsername("h00003968");
				SavedCredentialStore.RememberSessionBarePassword("secret-token");
				var command = new ShowAskPassWindowCommand();

				command.Execute("Username for 'https://example.com':", noPrompt: false, "", out string username);
				Assert.Equal("h00003968", username);
				Assert.Equal("h00003968", _store.FindEntry("example.com").Username);

				command.Execute("Password for 'https://h00003968@example.com':", noPrompt: false, "", out string password);
				Assert.Equal("secret-token", password);
				Assert.Equal("secret-token", _store.FindEntry("example.com").Password);

				// 落盘后：清会话缓存仍静默（host 记忆已建立，下次会话起永久静默）
				SavedCredentialStore.ClearSessionBareCredentialsForTests();
				command.Execute("Password for 'https://h00003968@example.com':", noPrompt: false, "", out string password2);
				Assert.Equal("secret-token", password2);
			}
			finally
			{
				SavedCredentialStore.SwapForTests(previous);
			}
		}

		[Fact]
		public void Command_GitMmProductionLogPrompts_AllAnswerSilentlyWithRememberedCredential()
		{
			// 图形化端到端回放（2026-09-15 生产日志 08:46-08:51，用户实测链路）：
			// 凭据已记（host + 账号 + 密码 + 不再询问，对应用户 credentials.json 现状）时，
			// 整个 git mm 询问序列必须全部静默、零弹窗——
			// 修复前：git 标准提示静默（08:46:48→49 仅 0.4s），但 git-mm 自有的小写
			// 无引号提示（08:47 起）解析不出 host → 反复弹窗（用户报障）。
			SavedCredentialStore previous = SavedCredentialStore.SwapForTests(_store);
			try
			{
				string host = "codehub-git-codeartsx.rnd.yinwang.com";
				string manifestPath = "innersource/T4VB_G/TNC/build/manifest.git";
				_store.RememberPassword(host, "h00003968", "!a654190921a");
				_store.SetNeverAsk(host, enabled: true);
				var command = new ShowAskPassWindowCommand();

				// 08:46:48 git 标准格式（带引号 + 尾随空格）
				command.Execute("Username for 'https://" + host + "/" + manifestPath + "': ", noPrompt: false, "/repo", out string gitUsername);
				Assert.Equal("h00003968", gitUsername);
				// 08:46:49 git 标准格式（URL 带 userinfo）
				command.Execute("Password for 'https://h00003968@" + host + "/" + manifestPath + "': ", noPrompt: false, "/repo", out string gitPassword);
				Assert.Equal("!a654190921a", gitPassword);
				// 08:47:05 git-mm 自有格式（小写、URL 无引号）——修复前落兜底弹窗
				command.Execute("username for https://" + host + "/" + manifestPath, noPrompt: false, "/repo", out string mmUsername);
				Assert.Equal("h00003968", mmUsername);
				// 08:47:13 git-mm 自有格式（空 userinfo https://@）——修复前无"记住密码"
				// 勾选、不落盘 → 反复弹
				command.Execute("password for https://@" + host + "/" + manifestPath, noPrompt: false, "/repo", out string mmPassword);
				Assert.Equal("!a654190921a", mmPassword);
				// 08:47:16 / 08:51:25 的重复询问同样静默（修复前反复弹窗的根因）
				command.Execute("username for https://" + host + "/" + manifestPath, noPrompt: false, "/repo", out string mmUsernameRepeat);
				Assert.Equal("h00003968", mmUsernameRepeat);
				command.Execute("password for https://@" + host + "/" + manifestPath, noPrompt: true, "/repo", out string mmPasswordRepeat);
				Assert.Equal("!a654190921a", mmPasswordRepeat);
			}
			finally
			{
				SavedCredentialStore.SwapForTests(previous);
			}
		}
	}
}
