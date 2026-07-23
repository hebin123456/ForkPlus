using System;
using System.Collections.Generic;
using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs
{
	// 阶段 3：承接 cherry-pick 父提交选择 + commit/-x 选项 + 多 sha 命令预览。
	// 合并提交（parents.Length>1）必须选 parent；非合并提交恒允许。
	// 多 commit 场景 shas 反转顺序后逐个 ToAbbreviatedString 拼接。
	internal sealed class CherryPickWindowViewModel
	{
		private readonly Revision[] _revisions;
		private readonly Sha[] _firstRevisionParents;
		private int _selectedParentIndex = -1;
		private bool _commit = true;
		private bool _appendOriginSha;

		public CherryPickWindowViewModel(Revision[] revisions, Sha[] firstRevisionParents)
		{
			_revisions = revisions;
			_firstRevisionParents = firstRevisionParents ?? Array.Empty<Sha>();
		}

		public bool MergeRevision => _firstRevisionParents.Length > 1;

		public int SelectedParentIndex
		{
			get => _selectedParentIndex;
			set => _selectedParentIndex = value;
		}

		public bool Commit
		{
			get => _commit;
			set => _commit = value;
		}

		public bool AppendOriginSha
		{
			get => _appendOriginSha;
			set => _appendOriginSha = value;
		}

		public bool IsSubmitAllowed => !MergeRevision || _selectedParentIndex >= 0;

		public string CommandPreview
		{
			get
			{
				if (_revisions == null || _revisions.Length == 0)
				{
					return null;
				}
				var parts = new List<string> { "git", "cherry-pick" };
				if (!_commit)
				{
					parts.Add("--no-commit");
				}
				if (_appendOriginSha)
				{
					parts.Add("-x");
				}
				if (MergeRevision)
				{
					int parentNumber = _selectedParentIndex + 1;
					if (parentNumber > 0)
					{
						parts.Add("-m " + parentNumber.ToString());
					}
				}
				Sha[] shas = _revisions.Map((Revision x) => x.Sha);
				Array.Reverse(shas);
				foreach (Sha sha in shas)
				{
					parts.Add(sha.ToAbbreviatedString());
				}
				return string.Join(" ", parts);
			}
		}
	}
}
