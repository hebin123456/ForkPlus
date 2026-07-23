using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// <see cref="PushTagWindow"/> 的 ViewModel（阶段 3 横向复用）。
	/// 承接 remote 选择校验 + git push tag 命令预览拼接。零 WPF using。
	/// </summary>
	/// <remarks>
	/// 新验证的模式点：<see cref="ForkPlusDialogWindow.IsSubmitAllowed"/> override 内调
	/// <c>base.IsSubmitAllowed</c>（基类有自己的提交前置条件）。拆分原则——VM 只判"remote 已选"，
	/// View 的 override 在 VM 返回 true 时再 <c>&amp;&amp; base.IsSubmitAllowed</c>。
	/// </remarks>
	public class PushTagWindowViewModel : INotifyPropertyChanged
	{
		private readonly Tag _tag;
		private Remote _selectedRemote;

		public PushTagWindowViewModel(Tag tag)
		{
			_tag = tag;
		}

		/// <summary>当前选中的 remote（View 把 ComboBox.SelectedItem 推到这里）。null 表示未选。</summary>
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

		/// <summary>remote 是否已选（替换原 override 的"SelectedItem is Remote"判断）。不含 base.IsSubmitAllowed，由 View 的 override 合并。</summary>
		public bool IsRemoteSelected => _selectedRemote != null;

		/// <summary>拼接 <c>git push &lt;remote&gt; &lt;tag&gt;</c> 预览。remote 未选时返回 null。</summary>
		public string CommandPreview
		{
			get
			{
				if (_selectedRemote == null)
				{
					return null;
				}
				List<string> parts = new List<string> { "git", "push", _selectedRemote.Name, _tag.FullReference };
				return string.Join(" ", parts);
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
