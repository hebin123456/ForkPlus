using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Threading;

namespace ForkPlus.UI.Commands
{
	public class QuickFetchCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Quick Fetch";

		public KeyGesture Shortcut => new KeyGesture(Key.F, global::Avalonia.Input.KeyModifiers.Alt | global::Avalonia.Input.KeyModifiers.Control | global::Avalonia.Input.KeyModifiers.Shift);

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule)
		{
			RepositoryData repositoryData = repositoryUserControl.RepositoryData;
			if (repositoryData != null)
			{
				Remote remote = IReadOnlyListExtensions.FirstItem(repositoryData.Remotes.Items, (Remote x) => x.Name == Consts.Git.DefaultRemoteName) ?? repositoryData.Remotes.Items.FirstItem();
				if (remote != null)
				{
					QuickFetch(repositoryUserControl, gitModule, remote);
				}
				else
				{
					new FetchWindow(repositoryUserControl, gitModule, remote).ShowDialog();
				}
			}
		}

		private void QuickFetch(RepositoryUserControl repositoryUserControl, GitModule gitModule, Remote remote)
		{
			bool fetchAllRemotes = ForkPlusSettings.Default.Fetch_FetchAllRemotes;
			bool fetchAllTags = ForkPlusSettings.Default.FetchAllTags;
			string name = (fetchAllRemotes ? PreferencesLocalization.Current("Fetch all") : PreferencesLocalization.FormatCurrent("Fetch '{0}'", remote.Name));
			// 防重入（2026-09-09，"QuickFetch 弹 cannot lock ref 错误窗"）：与后台自动 fetch
			//（AutomaticBackgroundFetchManager，FetchRemotesAutomatically 默认开启）并发时，
			// 两个 fetch 进程竞争 refs/remotes/* 的 git 乐观锁——后完成的一方 CAS 失败，
			// git 报 "cannot lock ref 'refs/remotes/...': is at X but expected Y" 并弹
			// ErrorWindow。同名 fetch 已在跑时直接跳过（在跑的那个做的是同一件事），
			// 与 AutomaticBackgroundFetchManager 的 FindJob 防护同款。
			if (repositoryUserControl.JobQueue.FindJob(name) != null)
			{
				Log.Info("Skip QuickFetch for '" + remote.Name + "' because a fetch job is already running");
				return;
			}
			repositoryUserControl.JobQueue.Add(name, delegate(JobMonitor monitor)
			{
				GitCommandResult fetchResult = new FetchGitCommand().Execute(gitModule, remote, fetchAllRemotes, monitor, noPrompt: false, fetchAllTags);
				repositoryUserControl.Dispatcher.Post(delegate
				{
					if (!fetchResult.Succeeded && !monitor.IsCanceled)
					{
						new ErrorWindow(repositoryUserControl, fetchResult.Error).ShowDialog();
					}
					repositoryUserControl.InvalidateAndRefresh(SubDomain.Revisions | SubDomain.References);
				});
			});
		}
	}
}
