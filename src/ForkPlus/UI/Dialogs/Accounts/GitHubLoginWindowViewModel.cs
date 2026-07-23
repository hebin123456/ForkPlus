using System.ComponentModel;
using System.Runtime.CompilerServices;
using ForkPlus.Accounts;

namespace ForkPlus.UI.Dialogs.Accounts
{
	/// <summary>
	/// <see cref="GitHubLoginWindow"/> 的 ViewModel（阶段 3 横向复用）。
	/// 承接认证类型选择 + token 非空校验。零 WPF using。
	/// </summary>
	/// <remarks>
	/// 复用 AccessToken 单输入模式。SetStatus(None,"") 副作用留 View override；
	/// View override 用 <c>_viewModel.IsSubmitAllowed &amp;&amp; base.IsSubmitAllowed</c> 合并基类守卫。
	/// </remarks>
	public class GitHubLoginWindowViewModel : INotifyPropertyChanged
	{
		private AuthenticationType? _selectedAuthenticationType;
		private string _token = string.Empty;

		/// <summary>当前选中的认证类型（null 表示未选）。</summary>
		public AuthenticationType? SelectedAuthenticationType
		{
			get => _selectedAuthenticationType;
			set
			{
				if (_selectedAuthenticationType != value)
				{
					_selectedAuthenticationType = value;
					OnPropertyChanged();
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

		/// <summary>选中 AccessToken 且 token 非空时允许提交（纯判断，副作用留 View override）。</summary>
		public bool IsSubmitAllowed
			=> _selectedAuthenticationType == AuthenticationType.AccessToken
				&& !string.IsNullOrEmpty(_token);

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
