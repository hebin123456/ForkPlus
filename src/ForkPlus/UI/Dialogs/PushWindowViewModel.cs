using System.Collections.Generic;
using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs
{
	// 阶段 3：承接 PushWindow 的远端/本地分支/远端分支选择 + 选项 + 命令预览。
	// RemoteItem/RemoteBranchItem 嵌套类（含 WPF ImageSource/Visibility）整体留 View 作列表项；
	// VM 仅持选中状态纯数据投影 + CustomRefspec + 选项 + 命令预览。
	// Refresh/RefreshRemoteBranches/RefreshRemotes/CheckSubmodules/SelectRemote 留 View。
	internal sealed class PushWindowViewModel
	{
		private LocalBranch _selectedLocalBranch;
		private Remote _selectedRemote;
		private RemoteBranch _selectedRemoteBranch;
		private string _customRefspec;
		private bool _pushAllTags;
		private bool _force;
		private bool _track;

		public LocalBranch SelectedLocalBranch
		{
			get => _selectedLocalBranch;
			set => _selectedLocalBranch = value;
		}

		public Remote SelectedRemote
		{
			get => _selectedRemote;
			set => _selectedRemote = value;
		}

		public RemoteBranch SelectedRemoteBranch
		{
			get => _selectedRemoteBranch;
			set => _selectedRemoteBranch = value;
		}

		public string CustomRefspec
		{
			get => _customRefspec;
			set => _customRefspec = value;
		}

		public bool PushAllTags
		{
			get => _pushAllTags;
			set => _pushAllTags = value;
		}

		public bool Force
		{
			get => _force;
			set => _force = value;
		}

		public bool Track
		{
			get => _track;
			set => _track = value;
		}

		public bool IsSubmitAllowed => _selectedLocalBranch != null && _selectedRemote != null;

		public string CommandPreview
		{
			get
			{
				if (_selectedRemote == null || _selectedLocalBranch == null)
				{
					return null;
				}
				var parts = new List<string> { "git", "push" };
				if (_force)
				{
					parts.Add("--force-with-lease");
				}
				if (_pushAllTags)
				{
					parts.Add("--tags");
				}
				if (_track)
				{
					parts.Add("--set-upstream");
				}
				parts.Add(_selectedRemote.Name);
				if (_selectedRemoteBranch != null)
				{
					string dst = (_selectedRemoteBranch.Remote == _selectedRemote.Name)
						? ("refs/heads/" + _selectedRemoteBranch.ShortName)
						: ("refs/heads/" + _selectedLocalBranch.Name);
					parts.Add(_selectedLocalBranch.FullReference + ":" + dst);
				}
				else if (_customRefspec != null)
				{
					parts.Add(_selectedLocalBranch.FullReference + ":" + _customRefspec);
				}
				else
				{
					parts.Add(_selectedLocalBranch.FullReference);
				}
				return string.Join(" ", parts);
			}
		}
	}
}
