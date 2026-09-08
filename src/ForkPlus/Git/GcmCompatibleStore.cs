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
		/// 查询 GCM 兼容键。命中时填出 username/password 并返回 true；未命中/非 Windows 返回 false。
		/// </summary>
		public static bool TryQuery([Null] string protocol, [Null] string host, [Null] string username, out string foundUsername, out string password)
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
			foreach (string targetName in BuildQueryTargetNames(protocol, host, username))
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
			if (!OperatingSystem.IsWindows() || string.IsNullOrEmpty(host) || string.IsNullOrEmpty(password))
			{
				return;
			}
			WindowsCredentialManager.WriteCredential(BuildStoreTargetName(protocol, host, username), username, password);
		}

		/// <summary>
		/// 删除 GCM 兼容键（宽容：user@host 与 host 级两个候选键都尝试删）。
		/// 非 Windows 为空操作。
		/// </summary>
		public static void Erase([Null] string protocol, [Null] string host, [Null] string username)
		{
			if (!OperatingSystem.IsWindows() || string.IsNullOrEmpty(host))
			{
				return;
			}
			foreach (string targetName in BuildQueryTargetNames(protocol, host, username))
			{
				WindowsCredentialManager.RemoveCredential(targetName);
			}
		}
	}
}
