using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace ForkPlus.Services.Avalonia
{
	/// <summary>
	/// 阶段 6：macOS 平台凭据存储，封装 Security framework Keychain API。
	///
	/// 用 <c>security</c> 命令行工具读写 Keychain，避免直接 P/Invoke Security.framework（
	/// SecItemAdd / SecItemCopyMatching 涉及 CFType/CFDictionary 等 CoreFoundation 类型，
	/// P/Invoke 复杂且 AOT 友好性差）。security CLI 自 macOS 10.0 起稳定，覆盖所有版本。
	///
	/// 命令映射：
	/// - 写入：security add-generic-password -a &lt;user&gt; -s &lt;service&gt; -w &lt;secret&gt; -U
	///   -U 表示更新已存在项（无 -U 时重复添加会报错）
	/// - 读取：security find-generic-password -a &lt;user&gt; -s &lt;service&gt; -w
	///   -w 输出 secret 到 stdout（不加 -w 输出 Keychain 项元数据）
	/// - 删除：security delete-generic-password -a &lt;user&gt; -s &lt;service&gt;
	///
	/// service 命名约定：com.forkplus.credential/&lt;key&gt;（与 Windows TargetName 风格一致）
	/// user 字段承载 username，secret 字段承载 password/passphrase。
	///
	/// SSH passphrase 与 SSH userpassword 的差异通过 service 后缀区分（见 SshPassphraseService /
	/// SshUserPasswordService），避免与 HTTP 凭据冲突。
	/// </summary>
	[SupportedOSPlatform("macos")]
	public class MacOsCredentialService : ICredentialService
	{
		private const string ServicePrefix = "com.forkplus.credential/";

		public Credential ReadCredential(string key)
		{
			if (string.IsNullOrEmpty(key))
			{
				return null;
			}
			try
			{
				// 先查 username（用 find-generic-password -g 输出元数据中的 "acct" 字段）
				// 再查 password（-w 输出到 stdout）。
				// 一次调用拿不到两个字段：security find-generic-password -a <user> -s <service> -w 需要 user。
				// 改用 -g（显示所有属性 + password 到 stderr）一次拿全。
				string service = ServicePrefix + key;
				(string user, string secret) = SecurityFindGenericPassword(service);
				if (secret != null)
				{
					return new Credential(CredentialType.Generic, "ForkPlus", user ?? string.Empty, secret);
				}
				return null;
			}
			catch (Exception ex)
			{
				Log.Error("MacOsCredentialService.ReadCredential failed for key '" + key + "'", ex);
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
				string service = ServicePrefix + key;
				SecurityAddGenericPassword(service, userName ?? string.Empty, secret ?? string.Empty);
			}
			catch (Exception ex)
			{
				Log.Error("MacOsCredentialService.WriteCredential failed for key '" + key + "'", ex);
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
				string service = ServicePrefix + key;
				return SecurityDeleteGenericPassword(service);
			}
			catch (Exception ex)
			{
				Log.Error("MacOsCredentialService.RemoveCredential failed for key '" + key + "'", ex);
				return false;
			}
		}

		public void StoreSshPassphrase(string sshKeyPath, string passphrase)
		{
			if (string.IsNullOrEmpty(sshKeyPath))
			{
				return;
			}
			// SSH passphrase 用独立 service 前缀，避免与 HTTP 凭据冲突
			WriteCredential(SshPassphraseKey(sshKeyPath), userName: "ssh-key", passphrase);
		}

		public string QuerySshPassphrase(string sshKeyPath)
		{
			if (string.IsNullOrEmpty(sshKeyPath))
			{
				return null;
			}
			return ReadCredential(SshPassphraseKey(sshKeyPath))?.Password;
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
			return ReadCredential(SshUserPasswordKey(url, username))?.Password;
		}

		// === security CLI 封装 ===
		// security add-generic-password -a <user> -s <service> -w <secret> -U
		// -U: update if exists（无 -U 时重复添加会失败 exit code != 0）
		// secret 通过 -w 参数传入（短 secret）或 stdin（长 secret / 含特殊字符）。
		// 这里用 -w 参数 + 引号转义，简单可靠；含换行的 secret 用 stdin 写入。
		private static void SecurityAddGenericPassword(string service, string user, string secret)
		{
			var psi = new ProcessStartInfo
			{
				FileName = "security",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardError = true,
			};
			psi.ArgumentList.Add("add-generic-password");
			psi.ArgumentList.Add("-a");
			psi.ArgumentList.Add(user);
			psi.ArgumentList.Add("-s");
			psi.ArgumentList.Add(service);
			// secret 含换行或空字符时无法通过 -w 传参，改用 stdin
			if (secret.IndexOf('\n') >= 0 || secret.IndexOf('\r') >= 0 || secret.IndexOf('\0') >= 0)
			{
				psi.ArgumentList.Add("-w");
				psi.RedirectStandardInput = true;
				using (var proc = Process.Start(psi))
				{
					if (proc == null) return;
					proc.StandardInput.Write(secret);
					proc.StandardInput.Close();
					proc.WaitForExit(5000);
				}
			}
			else
			{
				psi.ArgumentList.Add("-w");
				psi.ArgumentList.Add(secret);
				psi.ArgumentList.Add("-U");
				using (var proc = Process.Start(psi))
				{
					if (proc == null) return;
					proc.WaitForExit(5000);
				}
			}
		}

		// security find-generic-password -a <user> -s <service> -g
		// -g: 输出 password 到 stderr（macOS security 工具的奇怪设计：password 走 stderr 而非 stdout）
		// stdout 输出 Keychain 项属性，含 "acct"="<user>" 行（用户名）。
		// 不存在时 exit code=128（secErrorTypeItemNotFound），返回 null。
		private static (string user, string secret) SecurityFindGenericPassword(string service)
		{
			// 不带 -a 调用：让 security 按 -s（service）查找，从属性中读 acct（用户名）。
			// 这样一次调用同时拿 user + password。
			var psi = new ProcessStartInfo
			{
				FileName = "security",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			};
			psi.ArgumentList.Add("find-generic-password");
			psi.ArgumentList.Add("-s");
			psi.ArgumentList.Add(service);
			psi.ArgumentList.Add("-g");
			using (var proc = Process.Start(psi))
			{
				if (proc == null)
				{
					return (null, null);
				}
				// security 在交互式终端会弹窗要求授权，但 RedirectStandardError/CreateNoWindow
				// 下走 ACL 检查：首次访问 Keychain 项时可能弹"允许 X 访问 Y"对话框（用户授权后持久化）。
				string stdout = proc.StandardOutput.ReadToEnd();
				string stderr = proc.StandardError.ReadToEnd();
				if (!proc.WaitForExit(10000))
				{
					return (null, null);
				}
				// exit code 128 = item not found
				if (proc.ExitCode != 0)
				{
					return (null, null);
				}
				// 从 stderr 解析 password：行格式为 "password: \"<secret>\""
				// 注意：password 可能含转义字符（\n, \", \\），此处仅做基础反转义。
				string secret = ParsePasswordFromStderr(stderr);
				// 从 stdout 解析 username：行格式为 "acct: \"<user>\""
				string user = ParseAttributeFromStdout(stdout, "acct");
				return (user, secret);
			}
		}

		// security delete-generic-password -a <user> -s <service>
		// 不存在时 exit code != 0；本方法返回是否曾存在。
		// 不传 -a 时会删除该 service 下所有 user（可能有多个），通常我们一个 service 一个 user。
		private static bool SecurityDeleteGenericPassword(string service)
		{
			// 先用 find 检查存在性（拿到 user 后再 delete，避免 -a 参数不匹配）
			(string user, _) = SecurityFindGenericPassword(service);
			if (user == null)
			{
				return false;
			}
			var psi = new ProcessStartInfo
			{
				FileName = "security",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardError = true,
			};
			psi.ArgumentList.Add("delete-generic-password");
			psi.ArgumentList.Add("-a");
			psi.ArgumentList.Add(user);
			psi.ArgumentList.Add("-s");
			psi.ArgumentList.Add(service);
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

		// stderr 格式：
		//   password: "0123456789abcdef"
		// 或（含特殊字符）：
		//   password: "line1\nline2"
		private static string ParsePasswordFromStderr(string stderr)
		{
			const string prefix = "password: \"";
			int start = stderr.IndexOf(prefix);
			if (start < 0)
			{
				return null;
			}
			start += prefix.Length;
			// 从末尾找最后一个未转义的 "
			int end = stderr.Length - 1;
			while (end > start)
			{
				if (stderr[end] == '"' && (end == 0 || stderr[end - 1] != '\\'))
				{
					break;
				}
				end--;
			}
			if (end <= start)
			{
				return null;
			}
			return UnescapeCString(stderr.Substring(start, end - start));
		}

		// stdout 格式（keychain 属性）：
		//   "acct"="<user>"
		//   "svce"="<service>"
		//   ...
		private static string ParseAttributeFromStdout(string stdout, string attrName)
		{
			string marker = "\"" + attrName + "\"=\"";
			int start = stdout.IndexOf(marker);
			if (start < 0)
			{
				return null;
			}
			start += marker.Length;
			int end = start;
			while (end < stdout.Length)
			{
				if (stdout[end] == '"' && (end == 0 || stdout[end - 1] != '\\'))
				{
					break;
				}
				end++;
			}
			if (end >= stdout.Length)
			{
				return null;
			}
			return UnescapeCString(stdout.Substring(start, end - start));
		}

		// C 字符串反转义（security 输出用 C 风格转义）
		// 仅处理常见：\n \r \t \" \\ \0
		private static string UnescapeCString(string s)
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
						case 'n': sb.Append('\n'); i++; break;
						case 'r': sb.Append('\r'); i++; break;
						case 't': sb.Append('\t'); i++; break;
						case '"': sb.Append('"'); i++; break;
						case '\\': sb.Append('\\'); i++; break;
						case '0': sb.Append('\0'); i++; break;
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

		private static string SshPassphraseKey(string sshKeyPath)
		{
			return "ssh-passphrase:" + sshKeyPath;
		}

		private static string SshUserPasswordKey(string url, string username)
		{
			return "ssh-userpassword:" + (username ?? string.Empty) + "@" + url;
		}
	}
}
