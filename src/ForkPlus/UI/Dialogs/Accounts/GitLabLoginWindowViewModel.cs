using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ForkPlus.Settings;

namespace ForkPlus.UI.Dialogs.Accounts
{
	/// <summary>
	/// <see cref="GitLabLoginWindow"/> 的 ViewModel（阶段 3 横向复用）。
	/// 承接 server URL 规范化(TrimEnd) + 绝对 URI 校验 + token 非空校验。零 WPF using。
	/// </summary>
	/// <remarks>
	/// 复用 BitbucketServerLoginWindowViewModel 模式，差异：带 <c>_server</c> 标志——
	/// 非 server 模式 ServerUrl 固定为 "https://gitlab.com"，跳过 URI 校验。
	/// SetStatus / Hint.Enable / Hint.Disable 副作用留 View override。
	/// </remarks>
	public class GitLabLoginWindowViewModel : INotifyPropertyChanged
	{
		private readonly bool _server;

		private string _serverText = string.Empty;
		private string _token = string.Empty;

		public GitLabLoginWindowViewModel(bool server)
		{
			_server = server;
		}

		/// <summary>服务端原始输入（View 把 ServerTextBox.Text 推到这里，仅 server 模式有意义）。</summary>
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

		/// <summary>规范化后的服务端 URL。非 server 模式固定为 "https://gitlab.com"。</summary>
		public string ServerUrl
		{
			get
			{
				if (!_server)
				{
					return "https://gitlab.com";
				}
				return (_serverText ?? string.Empty).ToLower().TrimEnd(Consts.Chars.Slash);
			}
		}

		/// <summary>ServerUrl 是合法绝对 URI 时为 true（纯判断）。非 server 模式恒为 true。</summary>
		public bool IsUriValid => !_server || Uri.TryCreate(ServerUrl, UriKind.Absolute, out _);

		/// <summary>URI 合法（或非 server 模式）且 token 非空时允许提交（纯判断，副作用留 View override）。</summary>
		public bool IsSubmitAllowed => IsUriValid && !string.IsNullOrEmpty(_token);

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
