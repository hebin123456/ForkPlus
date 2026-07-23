using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// <see cref="SshPassphraseWindow"/> 的 ViewModel（阶段 3 横向复用）。
	/// 承接 passphrase 非空校验。零 WPF using。
	/// </summary>
	/// <remarks>
	/// 复用 RenameStash 模式：<see cref="ForkPlusDialogWindow.IsSubmitAllowed"/> override 内含
	/// <c>SetStatus(None,"")</c> 副作用，副作用留 View，纯判断进 VM。无 GetCommandPreview。
	/// </remarks>
	public class SshPassphraseWindowViewModel : INotifyPropertyChanged
	{
		private string _passphrase = string.Empty;

		/// <summary>当前 passphrase 输入。</summary>
		public string Passphrase
		{
			get => _passphrase;
			set
			{
				if (_passphrase != value)
				{
					_passphrase = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(IsSubmitAllowed));
				}
			}
		}

		/// <summary>passphrase 非空时允许提交（纯判断，副作用留 View override）。</summary>
		public bool IsSubmitAllowed => !string.IsNullOrWhiteSpace(_passphrase);

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
