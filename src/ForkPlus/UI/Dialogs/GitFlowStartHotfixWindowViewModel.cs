using System.ComponentModel;
using System.Runtime.CompilerServices;
using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// <see cref="GitFlowStartHotfixWindow"/> 的 ViewModel（阶段 3 横向复用）。
	/// 承接 base 分支选择校验 + hotfix 名校验（GitFlow 格式 + 重名）+ 命令预览。零 WPF using。
	/// </summary>
	/// <remarks>
	/// 复用 GenerateNewSshKeyWindowViewModel 的 Validate() 三元组模式：
	/// 无效名 → Error，重名 → Warning。SetStatus 副作用留 View override。
	/// </remarks>
	public class GitFlowStartHotfixWindowViewModel : INotifyPropertyChanged
	{
		private readonly GitFlowSettings _gitFlowSettings;
		private readonly LocalBranch[] _localBranches;

		private string _hotfixName = string.Empty;
		private LocalBranch _selectedBaseBranch;

		public GitFlowStartHotfixWindowViewModel(GitFlowSettings gitFlowSettings, LocalBranch[] localBranches)
		{
			_gitFlowSettings = gitFlowSettings;
			_localBranches = localBranches ?? System.Array.Empty<LocalBranch>();
		}

		/// <summary>当前 hotfix 名输入。</summary>
		public string HotfixName
		{
			get => _hotfixName;
			set
			{
				if (_hotfixName != value)
				{
					_hotfixName = value;
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
		/// 无效名 → Error，重名 → Warning，其他 → None。</summary>
		public (bool IsAllowed, ForkPlusDialogStatus Status, string StatusMessage) Validate()
		{
			if (_selectedBaseBranch == null)
			{
				return (false, ForkPlusDialogStatus.None, null);
			}
			if (string.IsNullOrEmpty(_hotfixName))
			{
				return (false, ForkPlusDialogStatus.None, null);
			}
			string invalid = ReferenceNameValidator.ValidateGitFlow(_hotfixName);
			if (invalid != null)
			{
				return (false, ForkPlusDialogStatus.Error, invalid);
			}
			string branchName = (_gitFlowSettings.HotfixPrefix + _hotfixName).ToLower();
			if (_localBranches.AnyItem((LocalBranch x) => x.Name.ToLower() == branchName))
			{
				return (false, ForkPlusDialogStatus.Warning, "Branch '" + branchName + "' already exists");
			}
			return (true, ForkPlusDialogStatus.None, null);
		}

		/// <summary>拼接 <c>git flow hotfix start &lt;name&gt; &lt;baseBranch&gt;</c> 预览。</summary>
		public string CommandPreview
		{
			get
			{
				if (string.IsNullOrWhiteSpace(_hotfixName))
				{
					return null;
				}
				if (_selectedBaseBranch == null)
				{
					return null;
				}
				return "git flow hotfix start " + _hotfixName + " " + _selectedBaseBranch.Name;
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
