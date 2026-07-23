using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// <see cref="PushMultipleTagsWindow"/> 的 ViewModel（阶段 3 横向复用）。
	/// 与 <see cref="PushTagWindowViewModel"/> 同构，区别仅在拼接多个 tag 引用。零 WPF using。
	/// </summary>
	public class PushMultipleTagsWindowViewModel : INotifyPropertyChanged
	{
		private readonly Tag[] _tags;
		private Remote _selectedRemote;

		public PushMultipleTagsWindowViewModel(Tag[] tags)
		{
			_tags = tags;
		}

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

		/// <summary>remote 是否已选。不含 base.IsSubmitAllowed，由 View 的 override 合并。</summary>
		public bool IsRemoteSelected => _selectedRemote != null;

		/// <summary>拼接 <c>git push &lt;remote&gt; &lt;tag1&gt; &lt;tag2&gt; ...</c> 预览。remote 未选时返回 null。</summary>
		public string CommandPreview
		{
			get
			{
				if (_selectedRemote == null)
				{
					return null;
				}
				List<string> parts = new List<string> { "git", "push", _selectedRemote.Name };
				foreach (Tag tag in _tags)
				{
					parts.Add(tag.FullReference);
				}
				return string.Join(" ", parts);
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
