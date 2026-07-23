using System.ComponentModel;
using System.Runtime.CompilerServices;
using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// <see cref="GitFlowStartFeatureWindow"/> 的 ViewModel（阶段 3 横向复用）。
	/// 承接 base 分支选择校验 + feature 名校验（GitFlow 格式 + 重名）+ 命令预览。零 WPF using。
	/// </summary>
	/// <remarks>
	/// 复用 GitFlowStartHotfixWindowViewModel 的 Validate() 三元组模式，差异：
	/// 无效名 → Warning（与 Release 一致，保留原始行为），Hotfix 则为 Error。
	/// SetStatus 副作用留 View override。
	/// </remarks>
	public class GitFlowStartFeatureWindowViewModel : INotifyPropertyChanged
	{
		private readonly GitFlowSettings _gitFlowSettings;
		private readonly LocalBranch[] _localBranches;

		private string _featureName = string.Empty;
		private LocalBranch _selectedBaseBranch;

		public GitFlowStartFeatureWindowViewModel(GitFlowSettings gitFlowSettings, LocalBranch[] localBranches)
		{
			_gitFlowSettings = gitFlowSettings;
			_localBranches = localBranches ?? System.Array.Empty<LocalBranch>();
		}

		/// <summary>当前 feature 名输入。</summary>
		public string FeatureName
		{
			get => _featureName;
			set
			{
				if (_featureName != value)
				{
					_featureName = value;
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
		/// 无效名 → Warning，重名 → Warning，其他 → None。</summary>
		public (bool IsAllowed, ForkPlusDialogStatus Status, string StatusMessage) Validate()
		{
			if (_selectedBaseBranch == null)
			{
				return (false, ForkPlusDialogStatus.None, null);
			}
			if (string.IsNullOrEmpty(_featureName))
			{
				return (false, ForkPlusDialogStatus.None, null);
			}
			string invalid = ReferenceNameValidator.ValidateGitFlow(_featureName);
			if (invalid != null)
			{
				return (false, ForkPlusDialogStatus.Warning, invalid);
			}
			string branchName = (_gitFlowSettings.FeaturePrefix + _featureName).ToLower();
			if (_localBranches.AnyItem((LocalBranch x) => x.Name.ToLower() == branchName))
			{
				return (false, ForkPlusDialogStatus.Warning, "Branch '" + branchName + "' already exists");
			}
			return (true, ForkPlusDialogStatus.None, null);
		}

		/// <summary>拼接 <c>git flow feature start &lt;name&gt; &lt;baseBranch&gt;</c> 预览。</summary>
		public string CommandPreview
		{
			get
			{
				if (string.IsNullOrWhiteSpace(_featureName))
				{
					return null;
				}
				if (_selectedBaseBranch == null)
				{
					return null;
				}
				return "git flow feature start " + _featureName + " " + _selectedBaseBranch.Name;
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
