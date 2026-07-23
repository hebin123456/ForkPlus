using System.ComponentModel;
using System.Runtime.CompilerServices;
using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// <see cref="GitFlowStartReleaseWindow"/> 的 ViewModel（阶段 3 横向复用）。
	/// 承接 base 分支选择校验 + release 名校验（GitFlow 格式 + 重名）+ 命令预览。零 WPF using。
	/// </summary>
	/// <remarks>
	/// 复用 GitFlowStartHotfixWindowViewModel 模式，差异：无效名 → Warning（Hotfix 用 Error）。
	/// SetStatus 副作用留 View override。
	/// </remarks>
	public class GitFlowStartReleaseWindowViewModel : INotifyPropertyChanged
	{
		private readonly GitFlowSettings _gitFlowSettings;
		private readonly LocalBranch[] _localBranches;

		private string _releaseName = string.Empty;
		private LocalBranch _selectedBaseBranch;

		public GitFlowStartReleaseWindowViewModel(GitFlowSettings gitFlowSettings, LocalBranch[] localBranches)
		{
			_gitFlowSettings = gitFlowSettings;
			_localBranches = localBranches ?? System.Array.Empty<LocalBranch>();
		}

		/// <summary>当前 release 名输入。</summary>
		public string ReleaseName
		{
			get => _releaseName;
			set
			{
				if (_releaseName != value)
				{
					_releaseName = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(CommandPreview));
				}
			}
		}

		/// <summary>当前选中的 base 分支（null 表示未选）。</summary>
		public LocalBranch SelectedBaseBranch
		{
			get => _selectedBaseBranch;
			set
			{
				if (_selectedBaseBranch != value)
				{
					_selectedBaseBranch = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(CommandPreview));
				}
			}
		}

		/// <summary>执行校验，返回 (是否允许提交, 状态级别, 状态消息)。
		/// 无效名 → Warning（与 Hotfix 的 Error 不同，保留原始行为），重名 → Warning。</summary>
		public (bool IsAllowed, ForkPlusDialogStatus Status, string StatusMessage) Validate()
		{
			if (_selectedBaseBranch == null)
			{
				return (false, ForkPlusDialogStatus.None, null);
			}
			if (string.IsNullOrEmpty(_releaseName))
			{
				return (false, ForkPlusDialogStatus.None, null);
			}
			string invalid = ReferenceNameValidator.ValidateGitFlow(_releaseName);
			if (invalid != null)
			{
				return (false, ForkPlusDialogStatus.Warning, invalid);
			}
			string branchName = (_gitFlowSettings.ReleasePrefix + _releaseName).ToLower();
			if (_localBranches.AnyItem((LocalBranch x) => x.Name.ToLower() == branchName))
			{
				return (false, ForkPlusDialogStatus.Warning, "Branch '" + branchName + "' already exists");
			}
			return (true, ForkPlusDialogStatus.None, null);
		}

		/// <summary>拼接 <c>git flow release start &lt;name&gt; &lt;baseBranch&gt;</c> 预览。</summary>
		public string CommandPreview
		{
			get
			{
				if (string.IsNullOrWhiteSpace(_releaseName))
				{
					return null;
				}
				if (_selectedBaseBranch == null)
				{
					return null;
				}
				return "git flow release start " + _releaseName + " " + _selectedBaseBranch.Name;
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
