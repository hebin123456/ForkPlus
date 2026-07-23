using System.Collections.Generic;

namespace ForkPlus.UI.Dialogs
{
	// 阶段 3：承接 git mm start 多参数构建 + IsSubmitAllowed + 命令预览。
	// 参数：branch / -j jobs / -g grepMode / --all 或 选中 subrepo 名列表 / --allow-tag / --allow-commit / --allow-no-track / --head。
	// GitMmSubrepoItem→Name 投影到 SelectedSubrepoNames 由 View 完成（避免 VM 引用 ForkPlus.UI.UserControls 命名空间）。
	// RestoreDialogOptions/SaveDialogOptions/SubreposDropDownButton 留 View。
	internal sealed class GitMmStartWindowViewModel
	{
		private string _branchName = string.Empty;
		private int _jobs = 8;
		private string _grepMode = "mixed";
		private bool _allSubrepos = true;
		private IReadOnlyList<string> _selectedSubrepoNames = System.Array.Empty<string>();
		private bool _allowTag;
		private bool _allowCommit;
		private bool _allowNoTrack;
		private bool _head;

		public string BranchName
		{
			get => _branchName;
			set => _branchName = value ?? string.Empty;
		}

		public int Jobs
		{
			get => _jobs;
			set => _jobs = value;
		}

		public string GrepMode
		{
			get => _grepMode;
			set => _grepMode = value ?? "mixed";
		}

		public bool AllSubrepos
		{
			get => _allSubrepos;
			set => _allSubrepos = value;
		}

		public IReadOnlyList<string> SelectedSubrepoNames
		{
			get => _selectedSubrepoNames;
			set => _selectedSubrepoNames = value ?? System.Array.Empty<string>();
		}

		public bool AllowTag
		{
			get => _allowTag;
			set => _allowTag = value;
		}

		public bool AllowCommit
		{
			get => _allowCommit;
			set => _allowCommit = value;
		}

		public bool AllowNoTrack
		{
			get => _allowNoTrack;
			set => _allowNoTrack = value;
		}

		public bool Head
		{
			get => _head;
			set => _head = value;
		}

		public bool IsSubmitAllowed
		{
			get
			{
				if (string.IsNullOrWhiteSpace(_branchName))
				{
					return false;
				}
				if (!_allSubrepos && _selectedSubrepoNames.Count == 0)
				{
					return false;
				}
				return true;
			}
		}

		public string[] CreateArgs()
		{
			var args = new List<string> { "start", _branchName.Trim() };
			args.Add("-j");
			args.Add(_jobs.ToString());
			if (!string.IsNullOrWhiteSpace(_grepMode) && _grepMode != "mixed")
			{
				args.Add("-g");
				args.Add(_grepMode);
			}
			if (_allSubrepos)
			{
				args.Add("--all");
			}
			else
			{
				args.AddRange(_selectedSubrepoNames);
			}
			if (_allowTag)
			{
				args.Add("--allow-tag");
			}
			if (_allowCommit)
			{
				args.Add("--allow-commit");
			}
			if (_allowNoTrack)
			{
				args.Add("--allow-no-track");
			}
			if (_head)
			{
				args.Add("--head");
			}
			return args.ToArray();
		}

		public string CommandPreview => GitMmCommandPreviewHelper.Format(CreateArgs());
	}
}
