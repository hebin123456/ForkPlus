using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;
using ForkPlus.Settings;

namespace ForkPlus.UI.Dialogs
{
	public partial class ResetBranchWindow : ForkPlusDialogWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly Revision _destination;

		[Null]
		private readonly LocalBranch _branch;

		private BranchResetType _resetType = BranchResetType.Mixed;

		protected override string GetCommandPreview()
		{
			string flag = _resetType switch
			{
				BranchResetType.Soft => "--soft",
				BranchResetType.Mixed => "--mixed",
				BranchResetType.Hard => "--hard",
				_ => null
			};
			if (flag == null)
			{
				return null;
			}
			if (_destination == null)
			{
				return null;
			}
			string sha = _destination.Sha.ToAbbreviatedString();
			if (string.IsNullOrEmpty(sha))
			{
				return null;
			}
			return "git reset " + flag + " " + sha;
		}

		public ResetBranchWindow(RepositoryUserControl repositoryUserControl, [Null] LocalBranch activeBranch, Revision destination)
		{
			InitializeComponent();
			// WPF→Avalonia 迁移回归修复：axaml 中 Mixed 的 IsSelected="True" 已移除（见 axaml 内
			// Migration note——XAML 本地值会在容器物化时触发 Avalonia 单选 toggle 反选，把已选中项
			// 清成 -1 并让 SelectionChanged 携带空 AddedItems）。初始选中改为在此编程式设置
			// （SelectedIndex 走 SelectionModel，容器物化时经 MarkContainerSelected/SetCurrentValue
			// 同步，不产生 IsSet 本地值、不反选、不产生空 AddedItems 事件），与 _resetType 默认值
			// 一致，SelectionChanged 同步 _resetType 与命令预览，关闭态正确显示 Mixed。
			ResetTypeCombobox.SelectedIndex = 1;
		// v4.0.12：重置类型下拉项国际化。基类 Loaded 的 ApplyAutomaticLocalization 不会递归
		// 进 ComboBox 未物化的下拉项（关闭态仅物化选中项、下拉弹层按需生成），这三个
		// TextBlock 的原油画 xaml 默认文本是英文。此处按选中的 UI 语言显式翻译
		// 类型名（Soft/Mixed/Hard）与说明文案（Keep all changes... / Discard...）。
		SoftResetTypeText.Text = PreferencesLocalization.Current("Soft");
		SoftResetDescriptionText.Text = PreferencesLocalization.Current("Keep all changes. Stage differences");
		MixedResetTypeText.Text = PreferencesLocalization.Current("Mixed");
		MixedResetDescriptionText.Text = PreferencesLocalization.Current("Keep all changes. Unstage differences");
		HardResetTypeText.Text = PreferencesLocalization.Current("Hard");
		HardResetDescriptionText.Text = PreferencesLocalization.Current("Discard all local changes");
		_repositoryUserControl = repositoryUserControl;
			LoseCommitsPreviewSection.RepositoryUserControl = _repositoryUserControl;
			_branch = activeBranch;
			_destination = destination;
			if (activeBranch != null)
		{
			base.DialogTitle = PreferencesLocalization.Current("Reset Current Branch to Revision");
			base.DialogDescription = PreferencesLocalization.FormatCurrent("Move the '{0}' branch HEAD to the selected revision", activeBranch.Name);
			ActiveBranchGitPointView.Value = activeBranch;
		}
		else
		{
			base.DialogTitle = PreferencesLocalization.Current("Reset HEAD to Revision");
			base.DialogDescription = PreferencesLocalization.Current("Move HEAD to the selected revision");
			ActiveBranchGitPointView.Value = new SymbolicReference("HEAD");
		}
		base.SubmitButtonTitle = PreferencesLocalization.Current("Reset");
			DestinationGitPointView.Value = _destination;
			// InitializeComponent 期间 AddCommandPreview 已执行，但此时 _destination 尚未赋值，
			// 导致首次 RefreshCommandPreview 返回 null 折叠了预览。此处补刷一次以显示默认命令。
			RefreshCommandPreview();
			// WS2.3：重置丢失预览——<目标>..<当前分支尖> 范围内当前侧独有的提交（重置后将从
			// 分支尖丢失）。构造期同步计算（本地 rev-list/log 很快）；任何失败静默隐藏，
			// 绝不让窗口构造抛异常（此时窗口尚未 Show）。目标 ref 固定于构造参数，无后续
			// 刷新链；ResetTypeCombobox 的选择变化不影响丢失集合（soft/mixed/hard 均移动分支引用）。
			try
			{
				RefreshLoseCommitsPreview();
			}
			catch
			{
				// 预览失败：保持隐藏即可
			}
		}

		// WS2.3：计算并填充丢失预览。to 侧：有活跃分支用分支 ref（无则 HEAD——分离头指针
		// 场景重置 HEAD 同样可能丢弃当前 HEAD 可达而目标不可达的提交）。
		private void RefreshLoseCommitsPreview()
		{
			GitModule gitModule = _repositoryUserControl?.GitModule;
			if (gitModule == null || _destination == null)
			{
				return;
			}
			string from = _destination.Sha.ToString();
			if (string.IsNullOrEmpty(from))
			{
				return;
			}
			string to = (_branch != null && !string.IsNullOrEmpty(_branch.FullReference)) ? _branch.FullReference : "HEAD";
			GitCommandResult<GetCommitsBetweenGitCommand.CommitsBetweenResult> result = new GetCommitsBetweenGitCommand().Execute(gitModule, from, to);
			if (!result.Succeeded || result.Result.Count <= 0)
			{
				return;
			}
			string summary = PreferencesLocalization.FormatCurrent("Resetting will lose {0} commits", result.Result.Count);
			LoseCommitsPreviewSection.SetCommits(summary,
				result.Result.Commits.Map((GetCommitsBetweenGitCommand.CommitPreview x) => new CommitsPreviewSection.Item(x.Sha, x.Subject, x.FullSha)));
			LoseCommitsPreviewSection.IsVisible = true;
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			base.OnKeyDown(e);
			if (e.Key == Key.S)
			{
				ResetTypeCombobox.SelectedIndex = 0;
			}
			else if (e.Key == Key.M)
			{
				ResetTypeCombobox.SelectedIndex = 1;
			}
			else if (e.Key == Key.H)
			{
				ResetTypeCombobox.SelectedIndex = 2;
			}
		}

		protected override void OnSubmit()
		{
			GitModule gitModule = _repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			// WS2.4：目标分支受保护 → 提交前拦截（DisableEditableControls 之前，取消时控件仍可用）。
			// 分离头指针（_branch == null）无分支名可匹配，不拦截。
			if (_branch != null && !ProtectedBranchConfirmWindow.Confirm(this, gitModule.Settings.ProtectedBranches, new string[1] { _branch.Name }))
			{
				return;
			}
			string branchName = _branch?.Name ?? "HEAD";
			BranchResetType resetType = _resetType;
			Sha destinationSha = _destination.Sha;
			string resetTypeName = GetResetTypeName(_resetType);
			SubmodulesToUpdate submodulesToUpdate = _repositoryUserControl.SubmodulesToUpdate();
			DisableEditableControls();
			_repositoryUserControl.AddUndoable(PreferencesLocalization.FormatCurrent("Reset '{0}' ({1})", branchName, resetTypeName), delegate(JobMonitor monitor)
		{
			base.Dispatcher.Post(delegate
			{
				SetStatus(ForkPlusDialogStatus.InProgress, "Resetting '" + branchName + "'...");
			});
			GitCommandResult resetBranchResult = new ResetCurrentBranchToRevisionGitCommand().Execute(gitModule, destinationSha, resetType, monitor);
			GitCommandResult updateSubmodulesResult = GitCommandResult.Success();
			if (submodulesToUpdate.Length > 0 && resetType == BranchResetType.Hard)
			{
				base.Dispatcher.Post(delegate
				{
					SetStatus(ForkPlusDialogStatus.InProgress, "Updating submodules...");
				});
				updateSubmodulesResult = new UpdateSubmodulesGitCommand().Execute(gitModule, submodulesToUpdate, monitor);
			}
			base.Dispatcher.Post(delegate
			{
				if (!resetBranchResult.Succeeded)
				{
					Close(resetBranchResult);
				}
				else if (!updateSubmodulesResult.Succeeded)
				{
					Close(updateSubmodulesResult);
				}
				else
				{
					Close(resetBranchResult);
				}
			});
			// 返回最终结果，让 AddUndoable 据此决定是否取消快照
			return resetBranchResult.Succeeded ? updateSubmodulesResult : resetBranchResult;
		});
	}

		private void ResetTypeCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			// 守卫空 AddedItems（选区清除事件：AddedItems 空/RemovedItems 非空）。此处理器在
			// 容器物化管线内同步执行，任何异常都会中断 PanelContainerGenerator 的物化循环，
			// 造成"下拉只剩前两项"的级联故障（2026-09-07 根因修复的一部分，见 axaml 注释）。
			if (e.AddedItems.Count == 0)
			{
				return;
			}
			ComboBoxItem comboBoxItem = e.AddedItems[0] as ComboBoxItem;
			if (comboBoxItem?.Tag is BranchResetType resetType)
			{
				_resetType = resetType;
				RefreshCommandPreview();
			}
		}

		public static string GetResetTypeName(BranchResetType resetType)
		{
			return resetType switch
			{
				BranchResetType.Mixed => "mixed", 
				BranchResetType.Hard => "hard", 
				BranchResetType.Soft => "soft", 
				_ => throw new Exception("Cannot reach here"), 
			};
		}

	}
}
