using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs
{
	// 阶段 3：承接 PullWindow 的远端/远端分支选择 + rebase/tags 选项 + 命令预览。
	// 异步加载 references（_referencesLoaded 标志）+ ActiveLocalBranch 非空 + 双 Combobox 选中。
	// LoadReferencesAndRefresh/Refresh 异步流程留 View。
	internal sealed class PullWindowViewModel
	{
		private LocalBranch _activeLocalBranch;
		private bool _referencesLoaded;
		private Remote _selectedRemote;
		private RemoteBranch _selectedRemoteBranch;
		private bool _rebase;
		private bool _allTags;

		public LocalBranch ActiveLocalBranch
		{
			get => _activeLocalBranch;
			set => _activeLocalBranch = value;
		}

		public bool ReferencesLoaded
		{
			get => _referencesLoaded;
			set => _referencesLoaded = value;
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

		public bool Rebase
		{
			get => _rebase;
			set => _rebase = value;
		}

		public bool AllTags
		{
			get => _allTags;
			set => _allTags = value;
		}

		public bool IsSubmitAllowed
		{
			get
			{
				if (_activeLocalBranch == null || !_referencesLoaded)
				{
					return false;
				}
				return _selectedRemote != null && _selectedRemoteBranch != null;
			}
		}

		public string CommandPreview
		{
			get
			{
				if (_selectedRemote == null)
				{
					return null;
				}
				var parts = new System.Collections.Generic.List<string> { "git", "pull" };
				parts.Add(_selectedRemote.Name);
				if (_selectedRemoteBranch != null)
				{
					parts.Add(_selectedRemoteBranch.ShortName);
				}
				if (_rebase)
				{
					parts.Add("--rebase");
				}
				if (_allTags)
				{
					parts.Add("--tags");
				}
				return string.Join(" ", parts);
			}
		}
	}
}
