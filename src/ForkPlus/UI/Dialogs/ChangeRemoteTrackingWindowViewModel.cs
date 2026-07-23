using System.ComponentModel;
using System.Runtime.CompilerServices;
using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// <see cref="ChangeRemoteTrackingWindow"/> 的 ViewModel（阶段 3 横向复用）。
	/// 承接"选中项与当前 upstream 比较"校验 + 命令预览拼接。零 WPF using。
	/// </summary>
	/// <remarks>
	/// 新模式点：View 的嵌套 <c>RemoteBranchItem</c> 类含 WPF <c>Visibility</c>，整体留 View 作列表项展示。
	/// VM 仅持 <see cref="LocalBranch"/>（WPF 无关 Git 类型）+ 选中项的纯数据投影
	/// （<see cref="SelectedRemoteBranch"/> + <see cref="IsNoTrackingSelected"/>），据此计算
	/// IsSubmitAllowed / CommandPreview。<c>Refresh</c>（构建列表）与 <c>ConfigureForStatus</c> 类 UI 操作留 View。
	/// </remarks>
	public class ChangeRemoteTrackingWindowViewModel : INotifyPropertyChanged
	{
		private readonly LocalBranch _localBranch;

		private RemoteBranch _selectedRemoteBranch;
		private bool _isNoTrackingSelected;

		public ChangeRemoteTrackingWindowViewModel(LocalBranch localBranch)
		{
			_localBranch = localBranch;
		}

		/// <summary>当前选中项对应的远端分支（NoTracking/无选中时为 null）。</summary>
		public RemoteBranch SelectedRemoteBranch
		{
			get => _selectedRemoteBranch;
			set
			{
				if (_selectedRemoteBranch != value)
				{
					_selectedRemoteBranch = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(IsSubmitAllowed));
					OnPropertyChanged(nameof(CommandPreview));
				}
			}
		}

		/// <summary>当前是否选中"No tracking"项（用于区分命令预览：unset-upstream vs set-upstream-to）。</summary>
		public bool IsNoTrackingSelected
		{
			get => _isNoTrackingSelected;
			set
			{
				if (_isNoTrackingSelected != value)
				{
					_isNoTrackingSelected = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(IsSubmitAllowed));
					OnPropertyChanged(nameof(CommandPreview));
				}
			}
		}

		/// <summary>选中项的 FullReference 与当前 upstream 不同时允许提交（纯判断）。
		/// NoTracking 项的 RemoteBranch 为 null，与无 upstream（UpstreamFullReference 为 null）比较时相等 → 不允许。</summary>
		public bool IsSubmitAllowed => _localBranch.UpstreamFullReference != _selectedRemoteBranch?.FullReference;

		/// <summary>拼接 git branch --unset-upstream / --set-upstream-to= 命令预览。
		/// 无选中（非 NoTracking 且无 RemoteBranch）返回 null。</summary>
		public string CommandPreview
		{
			get
			{
				string localName = _localBranch.Name;
				string Quote(string s) => s.Contains(" ") ? "\"" + s + "\"" : s;
				if (_isNoTrackingSelected)
				{
					return "git branch --unset-upstream " + Quote(localName);
				}
				RemoteBranch remoteBranch = _selectedRemoteBranch;
				if (remoteBranch == null)
				{
					return null;
				}
				return "git branch --set-upstream-to=" + remoteBranch.Remote + "/" + remoteBranch.ShortName + " " + Quote(localName);
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
