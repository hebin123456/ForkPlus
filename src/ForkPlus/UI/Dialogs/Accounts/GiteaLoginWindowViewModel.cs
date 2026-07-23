using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ForkPlus.UI.Dialogs.Accounts
{
	/// <summary>
	/// <see cref="GiteaLoginWindow"/> 的 ViewModel（阶段 3 横向复用）。
	/// 承接 server URL 规范化（TrimEnd）+ 绝对 URI 校验 + token 非空校验。零 WPF using。
	/// </summary>
	/// <remarks>
	/// 复用 GitHubEnterpriseLoginWindowViewModel 模式（结构完全同构）。SetStatus/Hint.Enable/Disable 副作用留 View。
	/// </remarks>
	public class GiteaLoginWindowViewModel : INotifyPropertyChanged
	{
		private string _serverText = string.Empty;
		private string _token = string.Empty;

		/// <summary>服务端原始输入（View 把 ServerTextBox.Text 推到这里）。</summary>
		public string ServerText
		{
			get => _serverText;
			set
			{
				if (_serverText != value)
				{
					_serverText = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(ServerUrl));
					OnPropertyChanged(nameof(IsUriValid));
					OnPropertyChanged(nameof(IsSubmitAllowed));
				}
			}
		}

		/// <summary>当前 token 输入。</summary>
		public string Token
		{
			get => _token;
			set
			{
				if (_token != value)
				{
					_token = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(IsSubmitAllowed));
				}
			}
		}

		/// <summary>规范化后的服务端 URL（小写 + 去尾斜杠）。</summary>
		public string ServerUrl => (_serverText ?? string.Empty).ToLower().TrimEnd(Consts.Chars.Slash);

		/// <summary>ServerUrl 是合法绝对 URI 时为 true（纯判断）。</summary>
		public bool IsUriValid => Uri.TryCreate(ServerUrl, UriKind.Absolute, out _);

		/// <summary>URI 合法且 token 非空时允许提交（纯判断，副作用留 View override）。</summary>
		public bool IsSubmitAllowed => IsUriValid && !string.IsNullOrEmpty(_token);

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
