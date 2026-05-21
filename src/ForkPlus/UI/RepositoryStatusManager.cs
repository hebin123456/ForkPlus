using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI
{
	internal class RepositoryStatusManager
	{
		private DateTime _lastUpdateTime = DateTime.Today.AddDays(-1.0);

		private readonly DelayedAction<object> _refreshAction;

		private bool _isRefreshing;

		private bool UpdateRequired
		{
			get
			{
				int automaticStatusUpdateInterval = ForkPlusSettings.Default.AutomaticStatusUpdateInterval;
				if (automaticStatusUpdateInterval <= 0)
				{
					return false;
				}
				return DateTime.UtcNow - _lastUpdateTime > TimeSpan.FromSeconds(automaticStatusUpdateInterval);
			}
		}

		public RepositoryStatusManager()
		{
			_refreshAction = new DelayedAction<object>(RefreshInternal, 3.0);
		}

		public void Refresh()
		{
			if (UpdateRequired)
			{
				_refreshAction.InvokeWithDelay(null);
			}
		}

		private void RefreshInternal(object _)
		{
			if (_isRefreshing)
			{
				return;
			}
			RepositoryUserControl activeRepositoryUserControl = MainWindow.ActiveRepositoryUserControl;
			if (activeRepositoryUserControl == null)
			{
				return;
			}
			RepositoryUserControl[] repositoryUserControls = Application.Current.TabManager().RepositoryUserControls;
			Dispatcher dispatcher = activeRepositoryUserControl.Dispatcher;
			List<GitModule> gitModulesToUpdate = new List<GitModule>(repositoryUserControls.Length);
			HashSet<string> seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			RepositoryUserControl[] array = repositoryUserControls;
			foreach (RepositoryUserControl repositoryUserControl in array)
			{
				if (repositoryUserControl != activeRepositoryUserControl && repositoryUserControl.GitModule != null && seenPaths.Add(repositoryUserControl.GitModule.Path))
				{
					gitModulesToUpdate.Add(repositoryUserControl.GitModule);
				}
			}
			if (gitModulesToUpdate.Count == 0)
			{
				return;
			}
			_lastUpdateTime = DateTime.UtcNow;
			_isRefreshing = true;
			MainWindow.Instance.JobQueue.Add(PreferencesLocalization.Current("Automatic status refresh"), delegate
			{
				try
				{
					foreach (GitModule gitModule in gitModulesToUpdate)
					{
						GitCommandResult<bool> response = new IsRepositoryDirtyGitCommand().Execute(gitModule);
						if (response.Succeeded)
						{
							dispatcher.Async(delegate
							{
								IReadOnlyListExtensions.FirstItem(repositoryUserControls, (RepositoryUserControl x) => x.GitModule == gitModule)?.UpdateIsDirtyState(response.Result);
							});
						}
					}
				}
				finally
				{
					_isRefreshing = false;
				}
			});
		}
	}
}
