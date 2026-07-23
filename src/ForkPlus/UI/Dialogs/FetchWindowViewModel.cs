using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ForkPlus.Git;
using ForkPlus.Settings;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// <see cref="FetchWindow"/> 的 ViewModel（阶段 3 横向复用）。
	/// 承接 remote 选择 + fetchAllRemotes 复选框状态 + git fetch 命令预览。零 WPF using。
	/// </summary>
	/// <remarks>
	/// 新模式点（组合）：<see cref="ForkPlusDialogWindow.IsSubmitAllowed"/> 判 SelectedItem != null
	/// （非 <c>is Remote</c>），且 <see cref="GetCommandPreview"/> 依赖复选框 + 全局设置
	/// (<c>ForkPlusSettings.Default.FetchAllTags</c>)。VM 读 Settings 层（允许），
	/// 复选框状态作为 VM 属性由 View 推入。
	/// </remarks>
	public class FetchWindowViewModel : INotifyPropertyChanged
	{
		private Remote _selectedRemote;
		private bool _fetchAllRemotes;

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

		/// <summary>是否 fetch 所有 remote（对应 FetchAllRemotesCheckBox）。</summary>
		public bool FetchAllRemotes
		{
			get => _fetchAllRemotes;
			set
			{
				if (_fetchAllRemotes != value)
				{
					_fetchAllRemotes = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(CommandPreview));
				}
			}
		}

		/// <summary>remote 是否已选（原 override 判 SelectedItem != null）。不含 base.IsSubmitAllowed，由 View 合并。</summary>
		public bool IsRemoteSelected => _selectedRemote != null;

		/// <summary>拼接 <c>git fetch [--all|&lt;remote&gt;] [--tags]</c> 预览。
		/// allTags 取自 <c>ForkPlusSettings.Default.FetchAllTags</c>（全局设置，非本窗体控件）。</summary>
		public string CommandPreview
		{
			get
			{
				List<string> parts = new List<string> { "git", "fetch" };
				if (FetchAllRemotes)
				{
					parts.Add("--all");
				}
				else
				{
					if (_selectedRemote == null)
					{
						return null;
					}
					parts.Add(_selectedRemote.Name);
				}
				if (ForkPlusSettings.Default.FetchAllTags)
				{
					parts.Add("--tags");
				}
				return string.Join(" ", parts);
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
