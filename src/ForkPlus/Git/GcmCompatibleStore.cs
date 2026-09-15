using System;

namespace ForkPlus.Git
{
	/// <summary>
	/// 凭据收编（Layer C）：GCM 兼容的 Windows Credential Manager 键读写。
	/// 设计见 docs/credential-popup-unification.md。
	///
	/// 键格式与 git-credential-manager 的 WindowsCredentialStore 一致，GCM 外部读写互通：
	/// - 有 username：git:{protocol}://{username}@{host}
	/// - 无 username：git:{protocol}://{host}
	///
	/// 查询用宽容匹配（user@host 与 host 两个候选键都试），迁移用户由 GCM 存下的
	/// 存量凭据（host 级或 user 级键）都能命中；写入按 GCM 规则——描述带 username
	/// （git 的 store/erase 输入必含）写 user@host 键。
	///
	/// 平台守卫：所有入口先判 <see cref="OperatingSystem.IsWindows()"/>，非 Windows
	/// 直接返回未命中/空操作——否则 Advapi32 P/Invoke 抛 DllNotFoundException，而
	/// AskPass 的 IPC 服务线程只捕获 IOException，异常会把服务线程带崩。
	/// </summary>
	public static class GcmCompatibleStore
	{
		/// <summary>
		/// 构造查询候选键（宽容顺序：带 username 的键优先，host 级兜底）。
		/// </summary>
		public static string[] BuildQueryTargetNames([Null] string protocol, [Null] string host, [Null] string username)
		{
			string scheme = protocol ?? "https";
			if (!string.IsNullOrEmpty(username))
			{
				return new string[2]
				{
					"git:" + scheme + "://" + username + "@" + host,
					"git:" + scheme + "://" + host
				};
			}
			return new string[1] { "git:" + scheme + "://" + host };
		}

		/// <summary>
		/// 修复（2026-09-14，"usehttppath 下查不到 GCM 存量凭据"）：带 path 的完整候选列表，
		/// 由具体到宽泛：path 级（GCM 在 credential.usehttppath=true 时的存储粒度——键含
		/// 仓库路径、不含 username，username 记在凭据条目上）→ user@host 级 → host 级。
		/// 与 <see cref="BuildQueryTargetNames(string, string, string)"/> 的顺序约定一致：
		/// 更具体的键在前，命中即返回；空 path 视为缺失，退化为 host 级行为。
		/// </summary>
		public static string[] BuildQueryTargetNames([Null] string protocol, [Null] string host, [Null] string username, [Null] string path)
		{
			string scheme = protocol ?? "https";
			System.Collections.Generic.List<string> names = new System.Collections.Generic.List<string>(4);
			if (!string.IsNullOrEmpty(path))
			{
				names.Add("git:" + scheme + "://" + host + "/" + path);
				if (!string.IsNullOrEmpty(username))
				{
					names.Add("git:" + scheme + "://" + username + "@" + host + "/" + path);
				}
			}
			if (!string.IsNullOrEmpty(username))
			{
				names.Add("git:" + scheme + "://" + username + "@" + host);
			}
			names.Add("git:" + scheme + "://" + host);
			return names.ToArray();
		}

		/// <summary>
		/// 构造写入键（GCM 规则：有 username 写 user@host，否则写 host 级）。
		/// </summary>
		public static string BuildStoreTargetName([Null] string protocol, [Null] string host, [Null] string username)
		{
			string scheme = protocol ?? "https";
			if (!string.IsNullOrEmpty(username))
			{
				return "git:" + scheme + "://" + username + "@" + host;
			}
			return "git:" + scheme + "://" + host;
		}

		/// <summary>
		/// 修复（2026-09-14，同 <see cref="BuildQueryTargetNames(string, string, string, string)"/>）：
		/// 带 path 的写入键。GCM 在 usehttppath=true 下按完整路径存储且键不含 username
		///（username 记在凭据条目的 UserName 字段）——照此规则写，GCM 在终端里也能读到
		/// ForkPlus 存的凭据（双向互通）。path 缺失时退化为 host 级规则。
		/// </summary>
		public static string BuildStoreTargetName([Null] string protocol, [Null] string host, [Null] string username, [Null] string path)
		{
			string scheme = protocol ?? "https";
			if (!string.IsNullOrEmpty(path))
			{
				return "git:" + scheme + "://" + host + "/" + path;
			}
			return BuildStoreTargetName(protocol, host, username);
		}

		/// <summary>
		/// 查询 GCM 兼容键。命中时填出 username/password 并返回 true；未命中/非 Windows 返回 false。
		/// </summary>
		public static bool TryQuery([Null] string protocol, [Null] string host, [Null] string username, out string foundUsername, out string password)
		{
			return TryQuery(protocol, host, username, null, out foundUsername, out password);
		}

		/// <summary>
		/// 修复（2026-09-14，"usehttppath 下查不到 GCM 存量凭据"）：带 path 的查询。
		/// 候选键含 path 级（GCM 在 usehttppath=true 时的存储粒度），先具体后宽泛。
		/// </summary>
		public static bool TryQuery([Null] string protocol, [Null] string host, [Null] string username, [Null] string path, out string foundUsername, out string password)
		{
			foundUsername = null;
			password = null;
			if (!OperatingSystem.IsWindows())
			{
				return false;
			}
			if (string.IsNullOrEmpty(host))
			{
				return false;
			}
			foreach (string targetName in BuildQueryTargetNames(protocol, host, username, path))
			{
				Credential credential = WindowsCredentialManager.ReadCredential(targetName);
				if (credential != null && !string.IsNullOrEmpty(credential.Password))
				{
					foundUsername = credential.UserName;
					password = credential.Password;
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// 把凭据写入 GCM 兼容键（GCM 外部可读）。非 Windows 为空操作（见设计文档权衡一节）。
		/// </summary>
		public static void Store([Null] string protocol, [Null] string host, [Null] string username, [Null] string password)
		{
			Store(protocol, host, username, null, password);
		}

		/// <summary>
		/// 修复（2026-09-14，"usehttppath 下查不到 GCM 存量凭据"）：带 path 的写入。
		/// 写两个键：path 级（GCM usehttppath 规则——GCM 在终端里能读到，双向互通）
		/// + host 级（沿用既有行为——同 host 其它无 path 上下文/ForkPlus 兜底查询可静默复用，
		/// 避免多仓工作区每仓各弹一次询问）。path 缺失时只写 host 级（与旧行为一致）。
		/// </summary>
		public static void Store([Null] string protocol, [Null] string host, [Null] string username, [Null] string path, [Null] string password)
		{
			if (!OperatingSystem.IsWindows() || string.IsNullOrEmpty(host) || string.IsNullOrEmpty(password))
			{
				return;
			}
			if (!string.IsNullOrEmpty(path))
			{
				WindowsCredentialManager.WriteCredential(BuildStoreTargetName(protocol, host, username, path), username, password);
			}
			WindowsCredentialManager.WriteCredential(BuildStoreTargetName(protocol, host, username), username, password);
		}

		/// <summary>
		/// 删除 GCM 兼容键（宽容：user@host 与 host 级两个候选键都尝试删）。
		/// 非 Windows 为空操作。
		/// </summary>
		public static void Erase([Null] string protocol, [Null] string host, [Null] string username)
		{
			Erase(protocol, host, username, null);
		}

		/// <summary>
		/// 修复（2026-09-14，同上）：带 path 的删除。git 在认证失败时按完整上下文（含 path）
		/// 调 erase——path 级与 host 级候选键都尝试删，防止失效密码残留在任一键上被反复
		/// 静默回填（get 命中旧密码 → 永远认证失败）。
		/// </summary>
		public static void Erase([Null] string protocol, [Null] string host, [Null] string username, [Null] string path)
		{
			if (!OperatingSystem.IsWindows() || string.IsNullOrEmpty(host))
			{
				return;
			}
			foreach (string targetName in BuildQueryTargetNames(protocol, host, username, path))
			{
				WindowsCredentialManager.RemoveCredential(targetName);
			}
		}
	}
}
