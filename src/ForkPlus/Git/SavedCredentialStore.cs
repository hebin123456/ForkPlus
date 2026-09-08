using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ForkPlus.Git
{
	/// <summary>
	/// 凭据记忆（Layer D）：跨平台的 HTTP(S) 凭据持久化与"不再询问"偏好。
	/// 设计见 docs/credential-popup-unification.md（Layer D 一节）。
	///
	/// 背景：Layer C 的 <see cref="GcmCompatibleStore"/> 只在 Windows 上生效（Advapi32），
	/// Linux/macOS 的 store/erase 一直是空操作——非 Windows 用户每次 fetch/push 都要
	/// 重输账号密码。本存储补齐该缺口，并落实凭据弹窗三档记忆语义：
	/// - 第一档（默认行为，无勾选框）：自动记住上次输入的账号，host → username 映射，
	///   Username 弹窗预填；
	/// - 第二档"记住密码"（显式勾选）：password 非空即"已记住"，下次仍弹窗但密码框
	///   预填（弹窗内自动填充，用户确认后提交）；
	/// - 第三档"记住密码 + 不再弹出"（显式勾选）：password + NeverAskAgain 标记，
	///   credential get / askpass 全链路静默回填（完全不弹窗），凭据缺失时快速失败，
	///   可在偏好设置的凭据页用开关重新打开。
	///
	/// 记录按 host 唯一（与 git credential helper 的 host 级语义对齐；password prompt
	/// 携带的 username 只作记录内容，不作键的一部分）。密码失效时 git 调 erase
	/// （App IPC mode 5）→ <see cref="ForgetPassword"/> 清密码但保留账号记忆与
	/// "不再询问"标记，下次弹窗仍预填账号。
	///
	/// 持久化：ForkDirectoryPath/credentials.json，FileHelper.AtomicWrite 落盘
	/// （跨设备 rename 安全）。明文密码是已知权衡——Linux 无跨桌面环境统一的
	/// keyring 抽象（libsecret 依赖 D-Bus session，CI/远程环境普遍缺失），
	/// 与 accounts.json 的既有处理一致，依赖用户主目录权限保护。
	/// 线程模型：AskPass IPC 线程（get/erase）与 UI 线程（弹窗/偏好页）并发访问，
	/// 所有变更方法持锁；查询方法返回快照。
	/// </summary>
	public class SavedCredentialStore
	{
		/// <summary>单条已保存凭据（host 唯一）。Password 非空 = 用户勾选过"记住密码"。</summary>
		public class SavedCredential
		{
			public string Host { get; set; }

			[Null]
			public string Username { get; set; }

			[Null]
			public string Password { get; set; }

			public bool NeverAskAgain { get; set; }

			public bool HasPassword
			{
				get
				{
					return !string.IsNullOrEmpty(Password);
				}
			}
		}

		// git askpass 的 HTTP(S) 询问格式（git 的 askpass 提示不带尾随空格的变体也兼容）：
		//   Username for 'https://example.com':
		//   Password for 'https://user@example.com':
		private static readonly Regex UsernamePromptRegex = new Regex("^Username for '([^']+)'", RegexOptions.Compiled);

		private static readonly Regex PasswordPromptRegex = new Regex("^Password for '([^']+)'", RegexOptions.Compiled);

		private readonly object _sync = new object();

		private readonly string _filePath;

		private List<SavedCredential> _entries;

		/// <summary>进程内单例（生产用默认路径；测试经 <see cref="SwapForTests"/> 注入隔离实例）。</summary>
		[Null]
		private static SavedCredentialStore _current;

		public static SavedCredentialStore Current
		{
			get
			{
				if (_current == null)
				{
					_current = new SavedCredentialStore(Path.Combine(global::ForkPlus.App.ForkDirectoryPath, "credentials.json"));
				}
				return _current;
			}
		}

		public SavedCredentialStore(string filePath)
		{
			_filePath = filePath ?? throw new ArgumentNullException("filePath");
			_entries = Load();
		}

		/// <summary>测试注入：替换单例并返回旧实例（还原用）。单线程 xUnit collection 下安全。</summary>
		internal static SavedCredentialStore SwapForTests(SavedCredentialStore replacement)
		{
			SavedCredentialStore previous = _current;
			_current = replacement;
			return previous;
		}

		// ============================ askpass prompt 解析 ============================

		/// <summary>解析 Username 询问（仅 HTTP(S)）。命中时给出 host。</summary>
		public static bool TryParseUsernamePrompt([Null] string prompt, out string host)
		{
			host = null;
			if (string.IsNullOrEmpty(prompt))
			{
				return false;
			}
			Match match = UsernamePromptRegex.Match(prompt);
			if (!match.Success)
			{
				return false;
			}
			return TryParseCredentialUrl(match.Groups[1].Value, out host, out string _);
		}

		/// <summary>解析 Password 询问（仅 HTTP(S)）。命中时给出 host 与 prompt 携带的 username（可空）。</summary>
		public static bool TryParsePasswordPrompt([Null] string prompt, out string host, [Null] out string username)
		{
			host = null;
			username = null;
			if (string.IsNullOrEmpty(prompt))
			{
				return false;
			}
			Match match = PasswordPromptRegex.Match(prompt);
			if (!match.Success)
			{
				return false;
			}
			return TryParseCredentialUrl(match.Groups[1].Value, out host, out username);
		}

		private static bool TryParseCredentialUrl(string url, out string host, [Null] out string username)
		{
			host = null;
			username = null;
			if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
			{
				return false;
			}
			if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
			{
				return false;
			}
			if (string.IsNullOrEmpty(uri.Host))
			{
				return false;
			}
			host = uri.Host;
			username = uri.UserInfo;
			return true;
		}

		// ============================ 查询 ============================

		/// <summary>按 host 查条目（快照）。无记录返回 null。</summary>
		[Null]
		public SavedCredential FindEntry([Null] string host)
		{
			if (string.IsNullOrEmpty(host))
			{
				return null;
			}
			lock (_sync)
			{
				return _entries.FirstOrDefault((SavedCredential e) => string.Equals(e.Host, host, StringComparison.OrdinalIgnoreCase));
			}
		}

		/// <summary>
		/// 第三档（记住密码 + 不再弹出）静默命中：password 与 NeverAskAgain 皆备时
		/// credential get / askpass 直接回填，完全不弹窗。
		/// 仅"记住密码"未开"不再弹出"（第二档）不命中——保持弹窗（密码框预填）；
		/// 仅记账号（第一档）同样不命中——弹窗预填账号。
		/// </summary>
		public bool TryGetSilentCredential([Null] string host, [Null] out string username, [Null] out string password)
		{
			username = null;
			password = null;
			if (string.IsNullOrEmpty(host))
			{
				return false;
			}
			lock (_sync)
			{
				SavedCredential entry = _entries.FirstOrDefault((SavedCredential e) => string.Equals(e.Host, host, StringComparison.OrdinalIgnoreCase));
				if (entry == null || !entry.HasPassword || !entry.NeverAskAgain)
				{
					return false;
				}
				username = entry.Username;
				password = entry.Password;
				return true;
			}
		}

		/// <summary>偏好设置页展示用：全部条目（按 host 排序的快照）。</summary>
		public List<SavedCredential> GetAll()
		{
			lock (_sync)
			{
				return _entries.OrderBy((SavedCredential e) => e.Host, StringComparer.OrdinalIgnoreCase).ToList();
			}
		}

		// ============================ 变更（均落盘） ============================

		/// <summary>记住账号（保留已有密码与"不再询问"标记）。</summary>
		public void RememberUsername([Null] string host, [Null] string username)
		{
			if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(username))
			{
				return;
			}
			lock (_sync)
			{
				FindOrCreate(host).Username = username;
				Save();
			}
		}

		/// <summary>记住密码（显式勾选语义：password 非空即已记住）。</summary>
		public void RememberPassword([Null] string host, [Null] string username, [Null] string password)
		{
			if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(password))
			{
				return;
			}
			lock (_sync)
			{
				SavedCredential entry = FindOrCreate(host);
				if (!string.IsNullOrEmpty(username))
				{
					entry.Username = username;
				}
				entry.Password = password;
				Save();
			}
		}

		/// <summary>设置"不再询问"标记（true 时凭据缺失不弹窗、快速失败）。</summary>
		public void SetNeverAsk([Null] string host, bool enabled)
		{
			if (string.IsNullOrEmpty(host))
			{
				return;
			}
			lock (_sync)
			{
				FindOrCreate(host).NeverAskAgain = enabled;
				Save();
			}
		}

		/// <summary>
		/// 偏好设置页整条写入（新增/编辑同源）：账号、密码、"不再弹出"一次性落盘。
		/// 空 username/password 按 null 存（清空语义）；三样皆空的条目直接删除
		/// （等价 Remove），避免垃圾条目堆积。
		/// </summary>
		public void Upsert([Null] string host, [Null] string username, [Null] string password, bool neverAsk)
		{
			if (string.IsNullOrEmpty(host))
			{
				return;
			}
			lock (_sync)
			{
				SavedCredential entry = FindOrCreate(host);
				entry.Username = (string.IsNullOrEmpty(username) ? null : username);
				entry.Password = (string.IsNullOrEmpty(password) ? null : password);
				entry.NeverAskAgain = neverAsk;
				if (entry.Username == null && entry.Password == null && !entry.NeverAskAgain)
				{
					_entries.Remove(entry);
				}
				Save();
			}
		}

		/// <summary>
		/// erase 联动：密码失效时清密码，保留账号记忆与"不再询问"标记。
		/// 密码与标记皆空且无账号记忆的条目顺手删除，避免垃圾条目堆积。
		/// </summary>
		public void ForgetPassword([Null] string host)
		{
			if (string.IsNullOrEmpty(host))
			{
				return;
			}
			lock (_sync)
			{
				SavedCredential entry = _entries.FirstOrDefault((SavedCredential e) => string.Equals(e.Host, host, StringComparison.OrdinalIgnoreCase));
				if (entry == null)
				{
					return;
				}
				entry.Password = null;
				if (!entry.NeverAskAgain && string.IsNullOrEmpty(entry.Username))
				{
					_entries.Remove(entry);
				}
				Save();
			}
		}

		/// <summary>删除单条凭据（偏好设置页"Remove"；下次操作重新弹窗）。</summary>
		public void Remove([Null] string host)
		{
			if (string.IsNullOrEmpty(host))
			{
				return;
			}
			lock (_sync)
			{
				SavedCredential entry = _entries.FirstOrDefault((SavedCredential e) => string.Equals(e.Host, host, StringComparison.OrdinalIgnoreCase));
				if (entry != null)
				{
					_entries.Remove(entry);
					Save();
				}
			}
		}

		/// <summary>全局"重新询问"开关：清除全部主机的"不再询问"标记（偏好设置页）。</summary>
		public void ClearAllNeverAsk()
		{
			lock (_sync)
			{
				bool changed = false;
				foreach (SavedCredential entry in _entries)
				{
					if (entry.NeverAskAgain)
					{
						entry.NeverAskAgain = false;
						changed = true;
					}
				}
				if (changed)
				{
					Save();
				}
			}
		}

		private SavedCredential FindOrCreate(string host)
		{
			SavedCredential entry = _entries.FirstOrDefault((SavedCredential e) => string.Equals(e.Host, host, StringComparison.OrdinalIgnoreCase));
			if (entry == null)
			{
				entry = new SavedCredential
				{
					Host = host
				};
				_entries.Add(entry);
			}
			return entry;
		}

		// ============================ 持久化 ============================

		private List<SavedCredential> Load()
		{
			try
			{
				if (!File.Exists(_filePath))
				{
					return new List<SavedCredential>();
				}
				string content = File.ReadAllText(_filePath);
				JArray array = JsonConvert.DeserializeObject(content) as JArray;
				List<SavedCredential> list = new List<SavedCredential>();
				if (array != null)
				{
					foreach (JObject obj in array.OfType<JObject>())
					{
						SavedCredential entry = new SavedCredential
						{
							Host = (string)obj["Host"],
							Username = (string)obj["Username"],
							Password = (string)obj["Password"],
							NeverAskAgain = (obj["NeverAskAgain"] as JValue)?.Value<bool>() ?? false
						};
						if (!string.IsNullOrEmpty(entry.Host))
						{
							list.Add(entry);
						}
					}
				}
				return list;
			}
			catch (Exception)
			{
				// 文件损坏/占用：按空表起步，写入时覆盖修复（与 ForkPlusSettings.Load 的容错口径一致）
				return new List<SavedCredential>();
			}
		}

		private void Save()
		{
			try
			{
				JArray array = new JArray();
				foreach (SavedCredential entry in _entries.OrderBy((SavedCredential e) => e.Host, StringComparer.OrdinalIgnoreCase))
				{
					array.Add(new JObject
					{
						{
							"Host",
							entry.Host
						},
						{
							"Username",
							string.IsNullOrEmpty(entry.Username) ? JValue.CreateNull() : new JValue(entry.Username)
						},
						{
							"Password",
							string.IsNullOrEmpty(entry.Password) ? JValue.CreateNull() : new JValue(entry.Password)
						},
						{
							"NeverAskAgain",
							new JValue(entry.NeverAskAgain)
						}
					});
				}
				string directory = Path.GetDirectoryName(_filePath);
				if (!string.IsNullOrEmpty(directory))
				{
					Directory.CreateDirectory(directory);
				}
				FileHelper.AtomicWrite(_filePath, array.ToString(Formatting.Indented));
			}
			catch (Exception ex)
			{
				global::ForkPlus.Log.Error("Failed to save credentials.json", ex);
			}
		}
	}
}
