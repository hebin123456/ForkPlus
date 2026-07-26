using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Text;

namespace ForkPlus.Services.Avalonia
{
	/// <summary>
	/// 阶段 6：Linux 平台凭据存储。
	///
	/// 策略（按可用性降级）：
	/// 1. libsecret / GNOME Keyring（首选）：通过 secret-tool 命令行工具读写，与桌面环境集成。
	///    secret-tool 是 libsecret-tools 包提供的 CLI，在 GNOME/XFCE/Cinnamon/MATE 上默认可用。
	///    KDE 早期用 KWallet，但 secret-tool 通过 DBus Secret Service API 兼容 KWallet 5+。
	/// 2. ~/.git-credentials 文件（fallback）：与 git credential-store 行为一致，0600 权限。
	///    纯文本存储，仅当 libsecret 不可用时使用。SSH passphrase 同样写入此文件（按 key 索引）。
	///
	/// 与 Windows CredentialManager 的差异：
	/// - libsecret 走 DBus 异步 API，但 secret-tool CLI 是同步阻塞，与现有同步接口 ICredentialService 兼容。
	/// - libsecret key 是任意的 attribute，不强制 schema；本实现用 "forkplus:credential:&lt;key&gt;" 命名。
	/// </summary>
	[SupportedOSPlatform("linux")]
	public class LinuxCredentialService : ICredentialService
	{
		// libsecret schema name 与 attribute key
		private const string SecretSchemaName = "com.forkplus.Credential";
		private const string SecretAttrKey = "key";

		// fallback 文件路径：~/.forkplus-credentials（JSON 行格式，0600 权限）。
		// 不复用 git 的 ~/.git-credentials，因为后者是 https://user:pass@host 格式，
		// 而我们的 key 是任意字符串（含 SSH passphrase、SSH user@url 等），格式不兼容。
		private static readonly string s_credentialsPath =
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".forkplus-credentials");

		private static readonly bool s_libsecretAvailable = CheckLibsecretAvailable();

		public Credential ReadCredential(string key)
		{
			if (string.IsNullOrEmpty(key))
			{
				return null;
			}
			try
			{
				if (s_libsecretAvailable)
				{
					(string user, string secret) = SecretToolLookup(key);
					if (secret != null)
					{
						return new Credential(CredentialType.Generic, "ForkPlus", user ?? string.Empty, secret);
					}
				}
				// fallback：从 ~/.git-credentials 按 user@host 风格查找
				return ReadCredentialFromFile(key);
			}
			catch (Exception ex)
			{
				Log.Error("LinuxCredentialService.ReadCredential failed for key '" + key + "'", ex);
				return null;
			}
		}

		public void WriteCredential(string key, string userName, string secret)
		{
			if (string.IsNullOrEmpty(key))
			{
				return;
			}
			try
			{
				if (s_libsecretAvailable)
				{
					SecretToolStore(key, userName, secret);
					return;
				}
				WriteCredentialToFile(key, userName, secret);
			}
			catch (Exception ex)
			{
				Log.Error("LinuxCredentialService.WriteCredential failed for key '" + key + "'", ex);
			}
		}

		public bool RemoveCredential(string key)
		{
			if (string.IsNullOrEmpty(key))
			{
				return false;
			}
			try
			{
				if (s_libsecretAvailable)
				{
					return SecretToolClear(key);
				}
				return RemoveCredentialFromFile(key);
			}
			catch (Exception ex)
			{
				Log.Error("LinuxCredentialService.RemoveCredential failed for key '" + key + "'", ex);
				return false;
			}
		}

		public void StoreSshPassphrase(string sshKeyPath, string passphrase)
		{
			if (string.IsNullOrEmpty(sshKeyPath))
			{
				return;
			}
			// SSH passphrase 用专门的 key 命名，避免与 HTTP 凭据冲突
			WriteCredential(SshPassphraseKey(sshKeyPath), userName: "ssh-key", passphrase);
		}

		public string QuerySshPassphrase(string sshKeyPath)
		{
			if (string.IsNullOrEmpty(sshKeyPath))
			{
				return null;
			}
			Credential cred = ReadCredential(SshPassphraseKey(sshKeyPath));
			return cred?.Password;
		}

		public void StoreSshUserPassword(string url, string username, string password)
		{
			if (string.IsNullOrEmpty(url))
			{
				return;
			}
			WriteCredential(SshUserPasswordKey(url, username), username ?? string.Empty, password);
		}

		public string QuerySshUserPassword(string url, string username)
		{
			if (string.IsNullOrEmpty(url))
			{
				return null;
			}
			Credential cred = ReadCredential(SshUserPasswordKey(url, username));
			return cred?.Password;
		}

		// === libsecret / secret-tool CLI 封装 ===
		private static bool CheckLibsecretAvailable()
		{
			try
			{
				var psi = new ProcessStartInfo
				{
					FileName = "secret-tool",
					Arguments = "--version",
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
				};
				using (var proc = Process.Start(psi))
				{
					if (proc == null)
					{
						return false;
					}
					proc.WaitForExit(3000);
					return proc.ExitCode == 0;
				}
			}
			catch (Exception ex) when (ex is Win32Exception || ex is InvalidOperationException)
			{
				// secret-tool 未安装
				return false;
			}
		}

		// secret-tool lookup --name='com.forkplus.Credential' key <key>
		// 输出：第一行 username，后续行 secret（多行 secret 用 \n 分隔）。
		private static (string user, string secret) SecretToolLookup(string key)
		{
			// 用 attribute 查找：secret-tool lookup <schema> <attr-key> <attr-value> ...
			var psi = new ProcessStartInfo
			{
				FileName = "secret-tool",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			};
			psi.ArgumentList.Add("lookup");
			psi.ArgumentList.Add(SecretSchemaName);
			psi.ArgumentList.Add(SecretAttrKey);
			psi.ArgumentList.Add(key);
			using (var proc = Process.Start(psi))
			{
				if (proc == null)
				{
					return (null, null);
				}
				// secret-tool lookup 在凭据不存在时 exit code=1，stdout 为空
				if (!proc.WaitForExit(5000))
				{
					return (null, null);
				}
				if (proc.ExitCode != 0)
				{
					return (null, null);
				}
				// secret-tool 输出 secret 内容（可能多行）。username 通过独立 attribute 存储，
				// 但 secret-tool lookup 只返回 secret，username 需要 secret-tool search 才能拿到 attribute。
				// 为简化：username 与 secret 用 "\n---\n" 分隔符编码进 secret 内容（见 SecretToolStore）。
				string content = proc.StandardOutput.ReadToEnd();
				return DecodeSecretContent(content);
			}
		}

		private static void SecretToolStore(string key, string userName, string secret)
		{
			// secret-tool store --label='label' <schema> <attr-key> <attr-value> ...
			// stdin 输入 secret 内容。username 编码进 secret 内容（前缀），避免额外 attribute 查询。
			string content = EncodeSecretContent(userName, secret);
			var psi = new ProcessStartInfo
			{
				FileName = "secret-tool",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardInput = true,
				RedirectStandardError = true,
			};
			psi.ArgumentList.Add("store");
			psi.ArgumentList.Add("--label=ForkPlus:" + key);
			psi.ArgumentList.Add(SecretSchemaName);
			psi.ArgumentList.Add(SecretAttrKey);
			psi.ArgumentList.Add(key);
			using (var proc = Process.Start(psi))
			{
				if (proc == null)
				{
					return;
				}
				proc.StandardInput.Write(content);
				proc.StandardInput.Close();
				proc.WaitForExit(5000);
			}
		}

		private static bool SecretToolClear(string key)
		{
			// secret-tool clear <schema> <attr-key> <attr-value>
			// exit code=0 且无输出表示已清除；不存在该 key 时也返回 0
			var psi = new ProcessStartInfo
			{
				FileName = "secret-tool",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardError = true,
			};
			psi.ArgumentList.Add("clear");
			psi.ArgumentList.Add(SecretSchemaName);
			psi.ArgumentList.Add(SecretAttrKey);
			psi.ArgumentList.Add(key);
			using (var proc = Process.Start(psi))
			{
				if (proc == null)
				{
					return false;
				}
				proc.WaitForExit(5000);
				return proc.ExitCode == 0;
			}
		}

		// secret-tool 输出的 secret 内容是用户名与密码的编码组合。
		// 格式：第一行 username，剩余行为 password（可能多行）。分隔符：\n===\n
		// 与 EncodeSecretContent 配对。
		private const string SecretSeparator = "\n===\n";

		private static string EncodeSecretContent(string userName, string secret)
		{
			return (userName ?? string.Empty) + SecretSeparator + (secret ?? string.Empty);
		}

		private static (string user, string secret) DecodeSecretContent(string content)
		{
			if (string.IsNullOrEmpty(content))
			{
				return (null, null);
			}
			// 末尾若有换行（secret-tool 输出习惯加 \n），先去掉单个尾换行再切分
			if (content.EndsWith("\n"))
			{
				content = content.Substring(0, content.Length - 1);
			}
			int idx = content.IndexOf(SecretSeparator);
			if (idx < 0)
			{
				// 旧数据或外部写入的纯 secret：作为 password 返回，username 留空
				return (string.Empty, content);
			}
			string user = content.Substring(0, idx);
			string secret = content.Substring(idx + SecretSeparator.Length);
			return (user, secret);
		}

		// === ~/.forkplus-credentials fallback ===
		// 格式：每行一个 JSON 对象 {"key":"...","user":"...","secret":"..."}
		// 用 JSON 而非 git 原生 https://user:pass@host 格式，因为我们的 key 是任意字符串（含 SSH passphrase）。
		// 0600 权限保护，仅当前用户可读写。
		//
		// 阶段 6 AOT 注意：手动 JSON 解析（避免 System.Text.Json 在 NativeAOT 下的 IL2026/IL3050 警告）。
		// Entry 结构简单（3 个 string 字段），手写解析比引入 JsonSerializerContext source generator 更轻量。
		private static Credential ReadCredentialFromFile(string key)
		{
			if (!File.Exists(s_credentialsPath))
			{
				return null;
			}
			try
			{
				foreach (string line in File.ReadAllLines(s_credentialsPath))
				{
					if (string.IsNullOrWhiteSpace(line))
					{
						continue;
					}
					Entry entry = ParseEntry(line);
					if (entry != null && entry.key == key)
					{
						return new Credential(CredentialType.Generic, "ForkPlus", entry.user ?? string.Empty, entry.secret ?? string.Empty);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Debug("ReadCredentialFromFile failed: " + ex.Message);
			}
			return null;
		}

		private static void WriteCredentialToFile(string key, string userName, string secret)
		{
			var entries = new System.Collections.Generic.List<Entry>();
			if (File.Exists(s_credentialsPath))
			{
				try
				{
					foreach (string line in File.ReadAllLines(s_credentialsPath))
					{
						if (string.IsNullOrWhiteSpace(line))
						{
							continue;
						}
						Entry existing = ParseEntry(line);
						if (existing != null && existing.key != key)
						{
							entries.Add(existing);
						}
					}
				}
				catch (Exception ex)
				{
					Log.Debug("ReadAllLines for WriteCredentialToFile failed, starting fresh: " + ex.Message);
				}
			}
			entries.Add(new Entry { key = key, user = userName, secret = secret });
			var sb = new StringBuilder();
			foreach (Entry e in entries)
			{
				sb.Append(SerializeEntry(e)).Append('\n');
			}
			WriteFileSecure(s_credentialsPath, sb.ToString());
		}

		private static bool RemoveCredentialFromFile(string key)
		{
			if (!File.Exists(s_credentialsPath))
			{
				return false;
			}
			bool removed = false;
			var entries = new System.Collections.Generic.List<Entry>();
			try
			{
				foreach (string line in File.ReadAllLines(s_credentialsPath))
				{
					if (string.IsNullOrWhiteSpace(line))
					{
						continue;
					}
					Entry existing = ParseEntry(line);
					if (existing != null)
					{
						if (existing.key == key)
						{
							removed = true;
						}
						else
						{
							entries.Add(existing);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Debug("ReadAllLines for RemoveCredentialFromFile failed: " + ex.Message);
				return false;
			}
			if (!removed)
			{
				return false;
			}
			var sb = new StringBuilder();
			foreach (Entry e in entries)
			{
				sb.Append(SerializeEntry(e)).Append('\n');
			}
			WriteFileSecure(s_credentialsPath, sb.ToString());
			return true;
		}

		// 手动 JSON 解析（AOT 友好）。格式：{"key":"...","user":"...","secret":"..."}
		// 仅解析我们写入的格式，不处理嵌套对象/数组/转义字符（key/user/secret 不含 "）。
		// 若 key/user/secret 含 " 或 \，需扩展转义处理；当前场景下凭据值不含这些字符。
		[Null]
		private static Entry ParseEntry(string json)
		{
			if (string.IsNullOrWhiteSpace(json) || !json.StartsWith("{") || !json.EndsWith("}"))
			{
				return null;
			}
			string key = ExtractJsonField(json, "key");
			string user = ExtractJsonField(json, "user");
			string secret = ExtractJsonField(json, "secret");
			if (key == null)
			{
				return null;
			}
			return new Entry { key = key, user = user ?? string.Empty, secret = secret ?? string.Empty };
		}

		// 从 JSON 字符串提取字段值。仅支持简单字符串值："field":"value"
		[Null]
		private static string ExtractJsonField(string json, string fieldName)
		{
			string marker = "\"" + fieldName + "\":\"";
			int start = json.IndexOf(marker, StringComparison.Ordinal);
			if (start < 0)
			{
				return null;
			}
			start += marker.Length;
			int end = json.IndexOf('"', start);
			if (end <= start)
			{
				return null;
			}
			return UnescapeJsonString(json.Substring(start, end - start));
		}

		// JSON 字符串反转义（处理 \" \\ \/ \n \r \t \uXXXX）
		private static string UnescapeJsonString(string s)
		{
			if (string.IsNullOrEmpty(s) || s.IndexOf('\\') < 0)
			{
				return s;
			}
			var sb = new StringBuilder(s.Length);
			for (int i = 0; i < s.Length; i++)
			{
				if (s[i] == '\\' && i + 1 < s.Length)
				{
					char next = s[i + 1];
					switch (next)
					{
						case '"': sb.Append('"'); i++; break;
						case '\\': sb.Append('\\'); i++; break;
						case '/': sb.Append('/'); i++; break;
						case 'n': sb.Append('\n'); i++; break;
						case 'r': sb.Append('\r'); i++; break;
						case 't': sb.Append('\t'); i++; break;
						case 'u':
							if (i + 5 < s.Length)
							{
								string hex = s.Substring(i + 2, 4);
								if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int code))
								{
									sb.Append((char)code);
									i += 5;
								}
								else
								{
									sb.Append('\\');
								}
							}
							else
							{
								sb.Append('\\');
							}
							break;
						default: sb.Append('\\'); break;
					}
				}
				else
				{
					sb.Append(s[i]);
				}
			}
			return sb.ToString();
		}

		// 手动 JSON 序列化（AOT 友好）。转义 " \ 和控制字符。
		private static string SerializeEntry(Entry entry)
		{
			return "{\"key\":\"" + EscapeJsonString(entry.key) +
			       "\",\"user\":\"" + EscapeJsonString(entry.user) +
			       "\",\"secret\":\"" + EscapeJsonString(entry.secret) + "\"}";
		}

		private static string EscapeJsonString(string s)
		{
			if (string.IsNullOrEmpty(s))
			{
				return string.Empty;
			}
			var sb = new StringBuilder(s.Length);
			foreach (char c in s)
			{
				switch (c)
				{
					case '"': sb.Append("\\\""); break;
					case '\\': sb.Append("\\\\"); break;
					case '\n': sb.Append("\\n"); break;
					case '\r': sb.Append("\\r"); break;
					case '\t': sb.Append("\\t"); break;
					default:
						if (c < 0x20)
						{
							sb.Append("\\u").Append(((int)c).ToString("x4"));
						}
						else
						{
							sb.Append(c);
						}
						break;
				}
			}
			return sb.ToString();
		}

		// 写文件并设 0600 权限（仅当前用户可读写），与 git credential-store 行为一致。
		// 不依赖 Mono.Unix（NuGet 包未必引用），统一用 chmod 命令。
		private static void WriteFileSecure(string path, string content)
		{
			File.WriteAllText(path, content);
			try
			{
				// chmod 600：owner read/write only。路径含空格时需引号。
				Process.Start("chmod", "600 \"" + path + "\"")?.WaitForExit(2000);
			}
			catch
			{
				// 权限设置失败不致命（仍能读写），仅记日志
				Log.Debug("Failed to set 0600 on " + path);
			}
		}

		private static string SshPassphraseKey(string sshKeyPath)
		{
			return "ssh-passphrase:" + sshKeyPath;
		}

		private static string SshUserPasswordKey(string url, string username)
		{
			return "ssh-userpassword:" + (username ?? string.Empty) + "@" + url;
		}

		private class Entry
		{
			public string key { get; set; }
			public string user { get; set; }
			public string secret { get; set; }
		}
	}
}
