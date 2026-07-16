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
	/// Fork 工作流同步冲突预检命令：
	/// 1. 查找名为 "upstream" 的远端（fork 工作流约定）；
	/// 2. 通过 JobQueue 后台执行 <see cref="CheckForkSyncStatusGitCommand"/>（含 fetch）；
	/// 3. 检测完成后弹出 <see cref="ForkSyncCheckWindow"/> 显示三态结果。
	/// </summary>
	public class CheckForkSyncCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Check Remote Sync...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, LocalBranch localBranch)
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

			// 约定：fork 工作流的"主仓"远端名为 "upstream"，fork 仓为 "origin"
			Remote upstreamRemote = FindUpstreamRemote(repositoryData.Remotes.Items);
			if (upstreamRemote == null)
			{
				System.Windows.MessageBox.Show(
					PreferencesLocalization.Current("No 'upstream' remote found. Please add a remote named 'upstream' pointing to the main repository."),
					PreferencesLocalization.Current("Remote Sync Status"),
					System.Windows.MessageBoxButton.OK,
					System.Windows.MessageBoxImage.Warning);
				return;
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

			string branchName = localBranch.Name;
			GitModule gitModule = repositoryUserControl.GitModule;
			Remote capturedUpstream = upstreamRemote;
			LocalBranch capturedBranch = localBranch;

			repositoryUserControl.JobQueue.Add(
				PreferencesLocalization.FormatCurrent("Checking remote sync: {0}/{1}", capturedUpstream.Name, branchName),
				delegate(JobMonitor monitor)
				{
					GitCommandResult<ForkSyncStatus> result = new CheckForkSyncStatusGitCommand().Execute(
						gitModule, capturedUpstream, capturedBranch, branchName, monitor);

					repositoryUserControl.Dispatcher.Async(delegate
					{
						if (monitor.IsCanceled)
						{
							return;
						}
						if (!result.Succeeded)
						{
							new ErrorWindow(repositoryUserControl, result.Error).ShowDialog();
							return;
						}

						// fetch 之后远端引用已更新，刷新引用面板
						repositoryUserControl.InvalidateAndRefresh(SubDomain.Revisions | SubDomain.References);

						ForkSyncCheckWindow window = new ForkSyncCheckWindow(
							repositoryUserControl, capturedUpstream, capturedBranch, branchName, result.Result);
						window.ShowDialog();
					});
				}, JobFlags.SaveToLog);
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
	}
}
