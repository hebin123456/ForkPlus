// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows.Threading → using Avalonia.Threading
// - DispatcherTimer → Avalonia.Threading.DispatcherTimer（API 兼容：Interval/Tick/Start）
// - Dispatcher 解析为 Avalonia.Threading.Dispatcher
// - dispatcher.Async / repositoryUserControl.Dispatcher.Async 保持（自定义扩展 DispatcherExtension.Async，内部转发 Dispatcher.Post）
using System;
using System.Threading;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI
{
	internal class AutomaticBackgroundFetchManager
	{
		private static readonly TimeSpan StartFetchInterval = TimeSpan.FromMinutes(1.0);

		private static readonly TimeSpan RecurringFetchInterval = TimeSpan.FromMinutes(10.0);

		private readonly DispatcherTimer _dispatcherTimer = new DispatcherTimer();

		public AutomaticBackgroundFetchManager()
		{
			_dispatcherTimer.Interval = StartFetchInterval;
			_dispatcherTimer.Tick += _dispatcherTimer_Tick;
			_dispatcherTimer.Start();
		}

		private void _dispatcherTimer_Tick(object sender, EventArgs e)
		{
			MainWindow instance = MainWindow.Instance;
			if (instance == null)
			{
				return;
			}
			if (instance.IsActive && _dispatcherTimer.Interval != StartFetchInterval)
			{
				Log.Info("Application is active. Delaying automatic fetch by 1 minute to not disturb the user.");
				_dispatcherTimer.Interval = StartFetchInterval;
				return;
			}
			_dispatcherTimer.Interval = RecurringFetchInterval;
			if (!ForkPlusSettings.Default.FetchRemotesAutomatically)
			{
				return;
			}
			Dispatcher dispatcher = instance.Dispatcher;
			if (dispatcher == null)
			{
				return;
			}
			bool fetchAllTags = ForkPlusSettings.Default.FetchAllTags;
			GitModule[] gitModulesToUpdate = instance.TabManager.RepositoryUserControls.Map((RepositoryUserControl x) => x.GitModule);
			instance.JobQueue.Add(PreferencesLocalization.Current("Automatic fetch"), delegate
			{
				GitModule[] array = gitModulesToUpdate;
				foreach (GitModule gitModule in array)
				{
					Log.Info("Automatically fetching '" + gitModule.RepositoryName + "'");
					GitCommandResult<RepositoryRemotes> gitCommandResult = new GetRemotesGitCommand().Execute(gitModule);
					if (gitCommandResult.Succeeded)
					{
						Remote[] remotes = gitCommandResult.Result.Items;
						bool noPrompt = true;
						bool fetchAllRemotes = false;
						dispatcher.Async(delegate
						{
							RepositoryUserControl repositoryUserControl = IReadOnlyListExtensions.FirstItem(MainWindow.Instance?.TabManager.RepositoryUserControls, (RepositoryUserControl x) => x.GitModule == gitModule);
							if (repositoryUserControl != null)
							{
								Remote[] array2 = remotes;
								foreach (Remote remote in array2)
								{
									if (!remote.DisableImplicitFetch)
									{
										string name = JobName(remote);
										if (repositoryUserControl.JobQueue.FindJob(name) != null)
										{
											Log.Info("Skip fetch for " + remote.Name + " in " + gitModule.RepositoryName + " because old request is still running");
											break;
										}
										repositoryUserControl.JobQueue.Add(name, delegate(JobMonitor mon1)
										{
											if (new FetchGitCommand().Execute(gitModule, remote, fetchAllRemotes, mon1, noPrompt, fetchAllTags).Succeeded)
											{
												repositoryUserControl.Dispatcher.Async(delegate
												{
													RepositoryUserControl activeRepositoryUserControl = MainWindow.ActiveRepositoryUserControl;
													if (activeRepositoryUserControl != null)
													{
														repositoryUserControl.Invalidate(SubDomain.Revisions | SubDomain.References);
														if (repositoryUserControl == activeRepositoryUserControl)
														{
															repositoryUserControl.InvalidateAndRefresh(SubDomain.None);
														}
													}
												});
											}
										}, JobFlags.SaveToLog | JobFlags.ShowOnToolbar | JobFlags.Background | JobFlags.LongRunning);
									}
								}
							}
						});
						Thread.Sleep(200);
					}
				}
			}, JobFlags.SaveToLog | JobFlags.ShowOnToolbar | JobFlags.LongRunning);
		}

		private static string JobName(Remote remote)
		{
			return PreferencesLocalization.FormatCurrent("Fetch '{0}'", remote.Name);
		}
	}
}
