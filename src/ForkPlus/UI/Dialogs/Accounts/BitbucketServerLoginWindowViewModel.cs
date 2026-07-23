using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ForkPlus.UI.Dialogs.Accounts
{
	/// <summary>
	/// <see cref="BitbucketServerLoginWindow"/> 的 ViewModel（阶段 3 横向复用）。
	/// 承接 server URL 规范化 + 绝对 URI 校验 + token 非空校验。零 WPF using。
	/// </summary>
	/// <remarks>
	/// 新模式点：IsSubmitAllowed override 内含 <c>SetStatus(None,"")</c> +
	/// <c>PersonalAccessTokenHint.Disable()</c> 副作用，且 URI 校验通过后调 <c>Hint.Enable()</c>。
	/// 拆分原则——VM 暴露 <see cref="IsUriValid"/>（纯判断），View override 据此决定 Hint.Enable/Disable；
	/// <see cref="ServerUrl"/> 规范化（ToLower + TrimSlash）从 View 移入 VM，OnSubmit 也从 VM 取。
	/// </remarks>
	public class BitbucketServerLoginWindowViewModel : INotifyPropertyChanged
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

		/// <summary>规范化后的服务端 URL（小写 + 去尾斜杠，替换原 View 的 ServerUrl 属性）。</summary>
		public string ServerUrl => (_serverText ?? string.Empty).ToLower().Trim(Consts.Chars.Slash);

		/// <summary>ServerUrl 是合法绝对 URI 时为 true（纯判断）。</summary>
		public bool IsUriValid => Uri.TryCreate(ServerUrl, UriKind.Absolute, out _);

		/// <summary>URI 合法且 token 非空时允许提交（纯判断，副作用留 View override）。</summary>
		public bool IsSubmitAllowed => IsUriValid && !string.IsNullOrEmpty(_token);

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
