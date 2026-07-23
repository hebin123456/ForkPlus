using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ForkPlus.UI.Dialogs.Accounts
{
	/// <summary>
	/// <see cref="OpenAiLoginWindow"/> 的 ViewModel（阶段 3 横向复用）。
	/// 承接 token 非空校验。零 WPF using。
	/// </summary>
	/// <remarks>
	/// 复用 SshPassphrase 模式：<see cref="ForkPlusDialogWindow.IsSubmitAllowed"/> override 内含
	/// <c>SetStatus(None,"")</c> 副作用，副作用留 View，纯判断进 VM。无 GetCommandPreview。
	/// </remarks>
	public class OpenAiLoginWindowViewModel : INotifyPropertyChanged
	{
		private string _token = string.Empty;

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

		/// <summary>token 非空时允许提交（纯判断，副作用留 View override）。</summary>
		public bool IsSubmitAllowed => !string.IsNullOrEmpty(_token);

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
