using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// <see cref="RevertRevisionWindow"/> 的 ViewModel（阶段 3 横向复用）。
	/// 承接"merge 提交需选父提交"校验 + 命令预览拼接。零 WPF using。
	/// </summary>
	/// <remarks>
	/// 简单模式：非 merge 提交恒允许；merge 提交需选中父提交（SelectedParentIndex &gt;= 0）。
	/// 冲突预检（RevertTestGitCommand）与 RevisionParent ComboBox 可见性切换留 View（ctor 内一次性）。
	/// </remarks>
	public class RevertRevisionWindowViewModel : INotifyPropertyChanged
	{
		private readonly Revision _revision;
		private readonly Sha[] _revisionParents;

		private bool _commit = true;
		private int _selectedParentIndex = -1;

		public RevertRevisionWindowViewModel(Revision revision, Sha[] revisionParents)
		{
			_revision = revision;
			_revisionParents = revisionParents ?? System.Array.Empty<Sha>();
		}

		/// <summary>是否为 merge 提交（父提交数 &gt; 1）。</summary>
		public bool MergeRevision => _revisionParents.Length > 1;

		/// <summary>是否带 --no-commit（默认 true = 提交）。</summary>
		public bool Commit
		{
			get => _commit;
			set
			{
				if (_commit != value)
				{
					_commit = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(CommandPreview));
				}
			}
		}

		/// <summary>当前选中的父提交索引（ComboBox.SelectedIndex，-1 表示未选）。</summary>
		public int SelectedParentIndex
		{
			get => _selectedParentIndex;
			set
			{
				if (_selectedParentIndex != value)
				{
					_selectedParentIndex = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(IsSubmitAllowed));
					OnPropertyChanged(nameof(CommandPreview));
				}
			}
		}

		/// <summary>非 merge 提交恒允许；merge 提交需选中父提交（纯判断）。</summary>
		public bool IsSubmitAllowed => !MergeRevision || _selectedParentIndex >= 0;

		/// <summary>拼接 <c>git revert [--no-commit] [-m N] &lt;sha&gt;</c> 预览。无 revision 时返回 null。</summary>
		public string CommandPreview
		{
			get
			{
				if (_revision == null)
				{
					return null;
				}
				var parts = new List<string> { "git", "revert" };
				if (!_commit)
				{
					parts.Add("--no-commit");
				}
				if (MergeRevision)
				{
					int parentNumber = _selectedParentIndex + 1;
					if (parentNumber > 0)
					{
						parts.Add("-m " + parentNumber.ToString());
					}
				}
				parts.Add(_revision.Sha.ToAbbreviatedString());
				return string.Join(" ", parts);
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
