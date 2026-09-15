using System;
using System.ComponentModel;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup;
using ForkPlus.Git;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Layout;
using Avalonia.Styling;
using ForkPlus.Settings;

namespace ForkPlus.UI.Dialogs
{
	public partial class AskPassWindow : ForkPlusDialogWindow
	{
		[Null]
		private AskPassRequest _askPassRequest;

		private string _arguments;

		[Null]
		private string _httpsUsernameHost;

		[Null]
		private string _httpsPasswordHost;

		[Null]
		private string _httpsPasswordUsername;

		// 修复（2026-09-14，"Username 弹窗显示成密码框"）：git-mm 等外部工具经 GIT_ASKPASS
		// 询问时提示词是单词 "username"/"password"（无 URL、小写），旧逻辑全部模式分支
		// 匹配不上 → 落 else 兜底（"密码："标签 + 密码框）——"username" 询问被显示成密码框，
		// 用户把密码输进用户名框 → 认证失败 → 循环弹窗。单词识别见构造函数。
		private bool _isBareUsernamePrompt;

		private bool _isBarePasswordPrompt;

		public string Result { get; private set; }

		public AskPassWindow(string arguments, string repositoryPath)
		{
			InitializeComponent();
			// 注意：此处不能调用 PreferencesLocalization.Apply——基类 ForkPlusDialogWindow 在
			// Loaded 时已自动本地化（ApplyAutomaticLocalization）。本窗口构造函数会按凭据
			// 询问模式改写 InputTextBlock.Text（User Name:/Passphrase:/Password:），若构造期
			// 先 Apply，会把 XAML 默认文本 "Password:" 缓存为 OriginalText，Loaded 时基类的
			// 二次 Apply 将从该缓存恢复，覆盖模式分支设置的文本（表现为用户名弹窗显示"密码："）。
			_askPassRequest = AskPassRequest.Parse(arguments);
			_arguments = arguments;
			RememberCheckBox.Hide();
			RememberPasswordCheckBox.Hide();
			NeverAskCheckBox.Hide();
			base.DialogTitle = ((repositoryPath != "") ? Path.GetFileName(repositoryPath) : PreferencesLocalization.Current("Credentials Required"));
			// 修复（2026-09-14，"弹窗内容未国际化"）：描述不再直接显示 git 英文原文，改用
			// 本地化格式串（"Username for '{0}':" 等，8 语言词条），{0} 保留引号内完整 URL。
			string trimmedPrompt = arguments.Trim();
			_isBareUsernamePrompt = string.Equals(trimmedPrompt, "username", StringComparison.OrdinalIgnoreCase);
			_isBarePasswordPrompt = string.Equals(trimmedPrompt, "password", StringComparison.OrdinalIgnoreCase);
			if (_arguments.StartsWith("Username for", StringComparison.OrdinalIgnoreCase))
			{
				base.DialogDescription = PreferencesLocalization.FormatCurrent("Username for '{0}':", ExtractPromptUrl(_arguments));
				InputTextBlock.Text = PreferencesLocalization.Current("User Name:");
				InputTextBox.Show();
				InputPasswordBox.Hide();
				InputTextBox.Focus();
				// 凭据记忆（Layer D）第一档：自动记住上次输入的账号（默认行为，无勾选框），
				// 本次询问预填已记住账号
				if (SavedCredentialStore.TryParseUsernamePrompt(_arguments, out _httpsUsernameHost))
				{
					SavedCredentialStore.SavedCredential saved = SavedCredentialStore.Current.FindEntry(_httpsUsernameHost);
					if (saved?.Username != null)
					{
						InputTextBox.Text = saved.Username;
					}
					else if (SavedCredentialStore.TryGetSessionBareUsername(out string sessionUsername))
					{
						// 会话桥接：同会话答过 git-mm 单词 username → 预填（正常路径已被
						// ShowAskPassWindowCommand 静默拦截，此处兜底直构场景）
						InputTextBox.Text = sessionUsername;
					}
				}
			}
			else if (_isBareUsernamePrompt)
			{
				// git-mm 的单词 "username" 询问：明文用户名框（提示无 URL，无法关联 host
				// 持久化——这也是单词弹窗没有"记住密码"勾选框的原因；会话级缓存见
				// SavedCredentialStore.SessionBare*，同会话只问一次）
				base.DialogDescription = PreferencesLocalization.Current("Username");
				InputTextBlock.Text = PreferencesLocalization.Current("User Name:");
				InputTextBox.Show();
				InputPasswordBox.Hide();
				InputTextBox.Focus();
				if (SavedCredentialStore.TryGetSessionBareUsername(out string cachedUsername))
				{
					InputTextBox.Text = cachedUsername;
				}
			}
			else if (_askPassRequest is AskPassRequest.SshPassphrase sshPassphrase)
			{
				base.DialogDescription = PreferencesLocalization.FormatCurrent("Passphrase for SSH key '{0}'", sshPassphrase.KeyPath);
				InputTextBlock.Text = PreferencesLocalization.Current("Passphrase:");
				InputTextBox.Hide();
				InputPasswordBox.Show();
				InputPasswordBox.Focus();
				RememberCheckBox.Show();
			}
			else if (_arguments.StartsWith("Enter passphrase"))
			{
				base.DialogDescription = _arguments;
				InputTextBlock.Text = PreferencesLocalization.Current("Passphrase:");
				InputTextBox.Hide();
				InputPasswordBox.Show();
				InputPasswordBox.Focus();
			}
			else if (_askPassRequest is AskPassRequest.SshUserPassword sshUserPassword)
			{
				base.DialogDescription = PreferencesLocalization.FormatCurrent("Passphrase for '{0}'", sshUserPassword.Username + "@" + sshUserPassword.Url.Host);
				InputTextBlock.Text = PreferencesLocalization.Current("Password:");
				InputTextBox.Hide();
				InputPasswordBox.Show();
				InputPasswordBox.Focus();
				RememberCheckBox.Show();
			}
			else if (SavedCredentialStore.TryParsePasswordPrompt(_arguments, out _httpsPasswordHost, out _httpsPasswordUsername))
			{
				// 凭据记忆（Layer D）第二档"记住密码"：密码已记住时预填密码框并预勾选。
				// 修复（2026-09-14，"勾了记住密码还不停询问"）：第二档并入静默回填——credential
				// get / askpass 对已记住密码直接回填不再弹窗（见 SavedCredentialStore/
				// ShowAskPassWindowCommand），"不再弹出"勾选与之等价、不再单独展示。
				base.DialogDescription = PreferencesLocalization.FormatCurrent("Password for '{0}':", ExtractPromptUrl(_arguments));
				InputTextBlock.Text = PreferencesLocalization.Current("Password:");
				InputTextBox.Hide();
				InputPasswordBox.Show();
				InputPasswordBox.Focus();
				SavedCredentialStore.SavedCredential saved = SavedCredentialStore.Current.FindEntry(_httpsPasswordHost);
				if (saved != null)
				{
					if (saved.HasPassword)
					{
						InputPasswordBox.Text = saved.Password;
						RememberPasswordCheckBox.IsChecked = true;
					}
				}
				else if (SavedCredentialStore.TryGetSessionBarePassword(out string sessionPassword))
				{
					// 会话桥接：同会话答过 git-mm 单词 password → 预填并预勾选（一次 OK
					// 即落盘 host 记忆，下次会话起静默）
					InputPasswordBox.Text = sessionPassword;
					RememberPasswordCheckBox.IsChecked = true;
				}
				RememberPasswordCheckBox.Show();
			}
			else if (_isBarePasswordPrompt)
			{
				// git-mm 的单词 "password" 询问：密码框（提示无 URL，无法关联 host 持久化；
				// 会话级缓存见 SavedCredentialStore.SessionBare*，同会话只问一次）
				base.DialogDescription = PreferencesLocalization.Current("Password");
				InputTextBlock.Text = PreferencesLocalization.Current("Password:");
				InputTextBox.Hide();
				InputPasswordBox.Show();
				InputPasswordBox.Focus();
			}
			else
			{
				base.DialogDescription = _arguments;
				InputTextBlock.Text = PreferencesLocalization.Current("Password:");
				InputTextBox.Hide();
				InputPasswordBox.Show();
				InputPasswordBox.Focus();
			}
			base.SubmitButtonTitle = PreferencesLocalization.Current("OK");
			// 修复（2026-09-14，"密码框还能输入中文"）：git HTTPS 密码/token 均为可打印 ASCII；
			// IME 汉字/全角输入一律过滤（TextChanged 兜底，同时覆盖键入与粘贴）。
			InputPasswordBox.TextChanged += FilterPasswordInputToAscii;
		}

		protected override void OnSubmit()
		{
			if (_arguments.StartsWith("Username for", StringComparison.OrdinalIgnoreCase) || _isBareUsernamePrompt)
			{
				// Username 询问统一取明文框（单词 "username" 提示此前误取密码框 → 回传空/错值）
				Result = InputTextBox.Text;
				if (_isBareUsernamePrompt && !string.IsNullOrEmpty(Result))
				{
					// git-mm 单词提示：写会话缓存（同会话后续静默复用，不再反复弹）
					SavedCredentialStore.RememberSessionBareUsername(Result);
				}
				// 凭据记忆（Layer D）第一档：自动记住上次输入的账号（默认行为，无勾选框）
				if (_httpsUsernameHost != null && !string.IsNullOrEmpty(Result))
				{
					SavedCredentialStore.Current.RememberUsername(_httpsUsernameHost, Result);
				}
			}
			else if (_httpsPasswordHost != null)
			{
				Result = InputPasswordBox.Text;
				if (NeverAskCheckBox.IsChecked.GetValueOrDefault() && !string.IsNullOrEmpty(Result))
				{
					// 第三档"记住密码 + 不再弹出"：密码落盘 + 标记，之后全链路静默回填
					SavedCredentialStore.Current.RememberPassword(_httpsPasswordHost, _httpsPasswordUsername, Result);
					SavedCredentialStore.Current.SetNeverAsk(_httpsPasswordHost, true);
				}
				else if (RememberPasswordCheckBox.IsChecked.GetValueOrDefault() && !string.IsNullOrEmpty(Result))
				{
					// 第二档"记住密码"：密码落盘静默复用（2026-09-14 起与第三档等效），
					// 顺带清残留的第三档标记
					SavedCredentialStore.Current.RememberPassword(_httpsPasswordHost, _httpsPasswordUsername, Result);
					SavedCredentialStore.Current.SetNeverAsk(_httpsPasswordHost, false);
				}
				else
				{
					// 不勾选：停止记住密码（清存量，账号记忆保留）
					SavedCredentialStore.Current.ForgetPassword(_httpsPasswordHost);
				}
			}
			else
			{
				Result = InputPasswordBox.Text;
				if (_isBarePasswordPrompt && !string.IsNullOrEmpty(Result))
				{
					// git-mm 单词提示：写会话缓存（同会话后续静默复用，不再反复弹）
					SavedCredentialStore.RememberSessionBarePassword(Result);
				}
			}
			if (_askPassRequest is AskPassRequest.SshPassphrase sshPassphrase)
			{
				if (RememberCheckBox.IsChecked.GetValueOrDefault())
				{
					WindowsCredentialManager.StoreSshPassphrase(sshPassphrase.KeyPath, Result);
				}
			}
			else if (_arguments.StartsWith("Enter passphrase"))
			{
				string text = AskPassParser.ParseSshKey(_arguments);
				if (!string.IsNullOrEmpty(text))
				{
					WindowsCredentialManager.StoreSshPassphrase(text, Result);
				}
			}
			else if (_askPassRequest is AskPassRequest.SshUserPassword sshUserPassword && RememberCheckBox.IsChecked.GetValueOrDefault())
			{
				WindowsCredentialManager.StoreSshUserPassword(sshUserPassword.Url, sshUserPassword.Username, Result);
			}
			Close();
		}

		/// <summary>提取提示词中的 URL：引号内优先（git 标准格式），无引号时取首个
		/// URL 词（git-mm 变体 "username for https://..."，2026-09-15 生产日志实锤）。
		/// 两者皆无时原样返回。</summary>
		private static string ExtractPromptUrl(string prompt)
		{
			int num = prompt.IndexOf('\'');
			if (num != -1)
			{
				int num2 = prompt.IndexOf('\'', num + 1);
				if (num2 != -1)
				{
					return prompt.Substring(num + 1, num2 - num - 1);
				}
			}
			System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(prompt, "https?://[^\\s']+");
			if (match.Success)
			{
				return match.Value;
			}
			return prompt;
		}

		/// <summary>密码框 ASCII 过滤：仅保留可打印 ASCII（0x20-0x7E），汉字/全角一律剔除。
		/// 重设 Text 会再触发一次 TextChanged，此时已全 ASCII、无操作退出，不构成循环。</summary>
		private void FilterPasswordInputToAscii(object sender, TextChangedEventArgs e)
		{
			string text = InputPasswordBox.Text;
			if (string.IsNullOrEmpty(text))
			{
				return;
			}
			bool hasNonAscii = false;
			foreach (char c in text)
			{
				if (c < ' ' || c > '~')
				{
					hasNonAscii = true;
					break;
				}
			}
			if (!hasNonAscii)
			{
				return;
			}
			System.Text.StringBuilder stringBuilder = new System.Text.StringBuilder(text.Length);
			foreach (char c in text)
			{
				if (c >= ' ' && c <= '~')
				{
					stringBuilder.Append(c);
				}
			}
			InputPasswordBox.Text = stringBuilder.ToString();
			InputPasswordBox.CaretIndex = stringBuilder.Length;
		}

	}
}
