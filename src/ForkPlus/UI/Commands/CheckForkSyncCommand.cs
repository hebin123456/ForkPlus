using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Commands
{
	/// <summary>
	/// 远端同步冲突预检命令：
	/// 1. 用户从二级菜单选择一个远端分支（或回退到本地分支同名）；
	/// 2. 立即弹出 <see cref="ForkSyncCheckWindow"/> 显示"检测中"，避免用户以为没反应；
	/// 3. 通过 JobQueue 后台执行 <see cref="CheckForkSyncStatusGitCommand"/>（含 fetch）；
	/// 4. 检测完成后调 <see cref="ForkSyncCheckWindow.UpdateResult"/> 刷新三态结果。
	/// </summary>
	public class CheckForkSyncCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Check Remote Sync...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		/// <summary>
		/// 旧入口：默认用本地分支名作为 upstream 目标分支名（fork 工作流约定同名）。
		/// 保留以兼容现有调用点，新代码应优先用 <see cref="Execute(RepositoryUserControl, LocalBranch, RemoteBranch)"/>。
		/// </summary>
		public void Execute(RepositoryUserControl repositoryUserControl, LocalBranch localBranch)
		{
			Execute(repositoryUserControl, localBranch, null);
		}

		/// <summary>
		/// 新入口：用户已从二级菜单选定远端分支 <paramref name="remoteBranch"/>（含远端名与分支名）。
		/// 传 null 时回退到用本地分支名在 upstream 远端查找（旧行为）。
		/// 立即弹框显示"检测中"，后台检测完成后更新结果。
		/// </summary>
		public void Execute(RepositoryUserControl repositoryUserControl, LocalBranch localBranch, RemoteBranch remoteBranch)
		{
			RepositoryData repositoryData = repositoryUserControl?.RepositoryData;
			if (repositoryData?.Remotes?.Items == null || repositoryData.Remotes.Items.Length == 0)
			{
				System.Windows.MessageBox.Show(
					PreferencesLocalization.Current("No remotes configured. Please add an upstream remote first."),
					PreferencesLocalization.Current("Remote Sync Status"),
					System.Windows.MessageBoxButton.OK,
					System.Windows.MessageBoxImage.Warning);
				return;
			}

			// 确定目标远端与目标分支名：优先用用户选定的 remoteBranch，否则回退到 upstream 远端 + 本地分支同名
			Remote upstreamRemote;
			string upstreamBranchName;
			if (remoteBranch != null)
			{
				upstreamRemote = FindRemoteByName(repositoryData.Remotes.Items, remoteBranch.Remote);
				upstreamBranchName = remoteBranch.ShortName;
				if (upstreamRemote == null)
				{
					System.Windows.MessageBox.Show(
						PreferencesLocalization.Current("No remotes configured. Please add an upstream remote first."),
						PreferencesLocalization.Current("Remote Sync Status"),
						System.Windows.MessageBoxButton.OK,
						System.Windows.MessageBoxImage.Warning);
					return;
				}
			}
			else
			{
				upstreamRemote = FindUpstreamRemote(repositoryData.Remotes.Items);
				if (upstreamRemote == null)
				{
					System.Windows.MessageBox.Show(
						PreferencesLocalization.Current("No 'upstream' remote found. Please add a remote named 'upstream' pointing to the main repository."),
						PreferencesLocalization.Current("Remote Sync Status"),
						System.Windows.MessageBoxButton.OK,
						System.Windows.MessageBoxImage.Warning);
					return;
				}
				upstreamBranchName = localBranch?.Name;
			}

			if (localBranch == null)
			{
				localBranch = repositoryData.References.ActiveBranch;
			}
			if (localBranch == null)
			{
				System.Windows.MessageBox.Show(
					PreferencesLocalization.Current("No active branch to check."),
					PreferencesLocalization.Current("Remote Sync Status"),
					System.Windows.MessageBoxButton.OK,
					System.Windows.MessageBoxImage.Warning);
				return;
			}

			// 确保远端分支名非空（remoteBranch 为 null 且 localBranch.Name 为空时）
			if (string.IsNullOrWhiteSpace(upstreamBranchName))
			{
				upstreamBranchName = localBranch.Name;
			}

			GitModule gitModule = repositoryUserControl.GitModule;
			Remote capturedUpstream = upstreamRemote;
			LocalBranch capturedBranch = localBranch;
			string capturedBranchName = upstreamBranchName;

			// 立即弹框显示"检测中"，让用户知道操作已触发，避免以为没反应。
			// 用 ShowDialog（模态）保持交互一致性；JobQueue 在后台线程跑检测，
			// 完成后通过 Dispatcher.Async 回调更新窗口内容（模态窗口不阻塞 Dispatcher 回调）。
			ForkSyncCheckWindow window = new ForkSyncCheckWindow(
				repositoryUserControl, capturedUpstream, capturedBranch, capturedBranchName, null);

			repositoryUserControl.JobQueue.Add(
				PreferencesLocalization.FormatCurrent("Checking remote sync: {0}/{1}", capturedUpstream.Name, capturedBranchName),
				delegate(JobMonitor monitor)
				{
					GitCommandResult<ForkSyncStatus> result = new CheckForkSyncStatusGitCommand().Execute(
						gitModule, capturedUpstream, capturedBranch, capturedBranchName, monitor);

					repositoryUserControl.Dispatcher.Async(delegate
					{
						if (monitor.IsCanceled)
						{
							return;
						}
						if (!result.Succeeded)
						{
							window.Close();
							new ErrorWindow(repositoryUserControl, result.Error).ShowDialog();
							return;
						}

						// fetch 之后远端引用已更新，刷新引用面板
						repositoryUserControl.InvalidateAndRefresh(SubDomain.Revisions | SubDomain.References);

						// 把对话框从"检测中"更新为最终结果
						window.UpdateResult(result.Result);
					});
				}, JobFlags.SaveToLog);

			// 先启动后台检测，再模态显示窗口——窗口会立即以"检测中"状态出现，
			// 检测完成时 Dispatcher 回调把结果刷进去。
			window.ShowDialog();
		}

		/// <summary>
		/// 查找 upstream 远端：优先返回名为 "upstream" 的远端；
		/// 若不存在，且只有一个非 origin 远端，也返回它（兼容自定义命名）。
		/// </summary>
		internal static Remote FindUpstreamRemote(Remote[] remotes)
		{
			foreach (Remote r in remotes)
			{
				if (r.Name == "upstream")
				{
					return r;
				}
			}
			// 兼容：没有显式命名为 upstream，但存在多个远端时，挑出第一个非 origin 的
			Remote fallback = null;
			int nonOriginCount = 0;
			foreach (Remote r in remotes)
			{
				if (r.Name != "origin")
				{
					fallback = r;
					nonOriginCount++;
				}
			}
			return nonOriginCount == 1 ? fallback : null;
		}

		/// <summary>按名称精确查找远端（用于二级菜单选定的远端分支反查远端对象）。</summary>
		internal static Remote FindRemoteByName(Remote[] remotes, string name)
		{
			if (string.IsNullOrWhiteSpace(name))
			{
				return null;
			}
			foreach (Remote r in remotes)
			{
				if (r.Name == name)
				{
					return r;
				}
			}
			return null;
		}
	}
}
