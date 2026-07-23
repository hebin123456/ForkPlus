using System.ComponentModel;
using System.Runtime.CompilerServices;
using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// <see cref="GitLfsPullWindow"/> 的 ViewModel（阶段 3 横向复用）。
	/// 与 <see cref="GitLfsFetchWindowViewModel"/> 同构，区别仅在命令前缀。零 WPF using。
	/// </summary>
	public class GitLfsPullWindowViewModel : INotifyPropertyChanged
	{
		private Remote _selectedRemote;

		/// <summary>当前选中的 remote。null 表示未选。</summary>
		public Remote SelectedRemote
		{
			get => _selectedRemote;
			set
			{
				if (_selectedRemote != value)
				{
					_selectedRemote = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(IsRemoteSelected));
					OnPropertyChanged(nameof(CommandPreview));
				}
			}
		}

		/// <summary>remote 是否已选。不含 base.IsSubmitAllowed，由 View override 合并。</summary>
		public bool IsRemoteSelected => _selectedRemote != null;

		/// <summary>拼接 <c>git lfs pull &lt;remote&gt;</c> 预览。remote 未选时返回 null。</summary>
		public string CommandPreview => _selectedRemote == null ? null : "git lfs pull " + _selectedRemote.Name;

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
