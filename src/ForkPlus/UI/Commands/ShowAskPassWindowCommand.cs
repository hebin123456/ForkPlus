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
			// 第三档（记住密码 + 不再弹出）→ 直接回填（credential get 层为主路径，此处兜底）；
			// "不再弹出"但密码缺失（失效被 erase）→ 快速失败（空响应），偏好设置 > Credentials
			// 的开关可重新打开。第二档（记住密码未开不再弹出）/第一档（仅记账号）→ 弹窗，
			// 由 AskPassWindow 预填密码/账号。
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
				if (entry != null && entry.NeverAskAgain)
				{
					if (!entry.HasPassword)
					{
						// "不再弹出"但密码缺失（被 erase）：快速失败，偏好设置开关可重新打开
						result = string.Empty;
						return;
					}
					if (isPasswordPrompt)
					{
						// 密码询问：回已记住的密码（与 prompt 是否带 userinfo 无关）
						result = entry.Password;
						return;
					}
					if (!string.IsNullOrEmpty(entry.Username))
					{
						// 账号询问：回已记住的账号（无账号可回则快速失败）
						result = entry.Username;
						return;
					}
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
