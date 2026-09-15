using System;
using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.UI.Commands
{
	public class ShowAskPassWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => null;

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(string request, bool noPrompt, string repositoryPath, out string result)
		{
			AskPassRequest askPassRequest = AskPassRequest.Parse(request);
			if (askPassRequest != null)
			{
				string text = QueryFromWindowsCredentialManager(askPassRequest);
				if (text != null)
				{
					result = text;
					return;
				}
			}
			// 凭据记忆（Layer D）：HTTP(S) 询问的静默路径——
			// "记住密码"（password 非空）→ 直接回填，完全不弹窗
			// （修复（2026-09-14，"勾了记住密码还不停询问"）：旧逻辑要求
			//   NeverAskAgain+password 双条件才静默，用户勾了"记住密码"仍每次弹窗；
			//   明文本就落盘，弹窗确认无安全增益，两档并入同一静默语义）；
			// "不再弹出"但密码缺失（失效被 erase）→ 快速失败（空响应），偏好设置 > Credentials
			// 的开关可重新打开。仅记账号（第一档）→ 弹窗预填账号。
			// （Username 与 Password 询问分别独立解析，绝不混判：见下——Password 询问的
			//   URL 可能不带 userinfo（promptUsername=null），若复用同一 host 分支回 Username
			//   （修复前），"不再弹出"的密码询问会误把主机账号当密码返回，认证必败。）
			string host = null;
			string promptUsername = null;
			bool isPasswordPrompt = SavedCredentialStore.TryParsePasswordPrompt(request, out host, out promptUsername);
			bool isUsernamePrompt = !isPasswordPrompt && SavedCredentialStore.TryParseUsernamePrompt(request, out host);
			if (isPasswordPrompt || isUsernamePrompt)
			{
				SavedCredentialStore.SavedCredential entry = SavedCredentialStore.Current.FindEntry(host);
				if (entry != null)
				{
					if (entry.HasPassword)
					{
						if (isPasswordPrompt)
						{
							// 密码询问：回已记住的密码（与 prompt 是否带 userinfo 无关）
							result = entry.Password;
							return;
						}
						if (!string.IsNullOrEmpty(entry.Username))
						{
							// 账号询问：回已记住的账号（无账号可回则弹窗）
							result = entry.Username;
							return;
						}
					}
					else if (entry.NeverAskAgain)
					{
						// "不再弹出"但密码缺失（被 erase）：快速失败，偏好设置开关可重新打开
						result = string.Empty;
						return;
					}
				}
				// 会话桥接（修复（2026-09-14，"单词提示答过又弹标准格式用户名/密码"）：
				// 同一 git mm 会话内已答过单词提示（git-mm 自有询问，无 URL）→ 标准格式
				// 询问在 host 无记忆时静默复用会话答案，并顺带落盘 host 记忆——同会话
				// 的询问几乎必然是同一 SSO 身份，下次会话起永久静默
				if (isPasswordPrompt && string.IsNullOrEmpty(entry?.Password)
					&& SavedCredentialStore.TryGetSessionBarePassword(out string sessionPassword))
				{
					SavedCredentialStore.Current.RememberPassword(host, promptUsername, sessionPassword);
					result = sessionPassword;
					return;
				}
				if (isUsernamePrompt && string.IsNullOrEmpty(entry?.Username)
					&& SavedCredentialStore.TryGetSessionBareUsername(out string sessionUsername))
				{
					SavedCredentialStore.Current.RememberUsername(host, sessionUsername);
					result = sessionUsername;
					return;
				}
			}
			// git-mm 单词提示（"username"/"password"，无 URL）：会话级缓存——首次弹窗
			// （AskPassWindow OnSubmit 写缓存），同会话后续静默复用，不再反复弹
			// （修复（2026-09-14，"再分别弹了一遍用户名和密码"））
			string trimmedRequest = request == null ? "" : request.Trim();
			bool isBareUsernamePrompt = string.Equals(trimmedRequest, "username", StringComparison.OrdinalIgnoreCase);
			bool isBarePasswordPrompt = string.Equals(trimmedRequest, "password", StringComparison.OrdinalIgnoreCase);
			if (isBareUsernamePrompt || isBarePasswordPrompt)
			{
				if (isBareUsernamePrompt && SavedCredentialStore.TryGetSessionBareUsername(out string cachedUsername))
				{
					result = cachedUsername;
					return;
				}
				if (isBarePasswordPrompt && SavedCredentialStore.TryGetSessionBarePassword(out string cachedPassword))
				{
					result = cachedPassword;
					return;
				}
				if (noPrompt)
				{
					// 后台周期（无会话缓存且用户未答过）：不弹窗，快速失败
					result = string.Empty;
					return;
				}
			}
			if (noPrompt)
			{
				result = string.Empty;
				return;
			}
			AskPassWindow askPassWindow = new AskPassWindow(request, repositoryPath);
			askPassWindow.ShowDialog();
			result = askPassWindow.Result ?? string.Empty;
		}

		[Null]
		private string QueryFromWindowsCredentialManager(AskPassRequest askPassRequest)
		{
			// 平台守卫（凭据收编 Layer C 顺带修复）：WCM 的 Advapi32 P/Invoke 在非
			// Windows 上抛 DllNotFoundException，而调用链（AskPass IPC 服务线程 →
			// UIThread.Sync）只捕获 IOException，一次 SSH 密钥 askpass 就会把 IPC 线程带崩。
			if (!OperatingSystem.IsWindows())
			{
				return null;
			}
			if (askPassRequest is AskPassRequest.SshPassphrase sshPassphrase)
			{
				string text = WindowsCredentialManager.QuerySshPassphrase(sshPassphrase.KeyPath);
				if (string.IsNullOrEmpty(text))
				{
					return null;
				}
				return text;
			}
			if (askPassRequest is AskPassRequest.SshUserPassword sshUserPassword)
			{
				string text2 = WindowsCredentialManager.QuerySshUserPassword(sshUserPassword.Url, sshUserPassword.Username);
				if (string.IsNullOrEmpty(text2))
				{
					return null;
				}
				return text2;
			}
			return null;
		}
	}
}
