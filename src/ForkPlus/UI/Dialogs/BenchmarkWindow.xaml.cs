using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.Services;

namespace ForkPlus.UI.Dialogs
{
	public partial class BenchmarkWindow : ForkPlusDialogWindow
	{
		private RepositoryUserControl _repositoryUserControl;

		[Null]
		private Job _activeJob;

		private string _benchmarkLog;

		public BenchmarkWindow(RepositoryUserControl repositoryUserControl)
		{
			_repositoryUserControl = repositoryUserControl;
			base.ShowLogo = false;
			base.ShowFooter = false;
			base.ShowHeader = false;
			InitializeComponent();
			MeasurementsContainer.Hide();
			PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			_activeJob?.Monitor.Cancel();
			_activeJob = null;
			base.OnClosing(e);
		}

		private void CopyResultButton_Click(object sender, RoutedEventArgs e)
		{
			ServiceLocator.Clipboard.SetText(_benchmarkLog);
		}

		private void StartButton_Click(object sender, RoutedEventArgs e)
		{
			StartButton.Collapse();
			MeasurementsContainer.Show();
			Refresh();
		}

		private void RefreshButton_Click(object sender, RoutedEventArgs e)
		{
			Refresh();
		}

		private void Refresh()
		{
			_activeJob?.Monitor.Cancel();
			RefreshControls(null, null);
			RepositoryUserControl repositoryUserControl = _repositoryUserControl;
			GitModule gitModule = _repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			_activeJob = _repositoryUserControl.JobQueue.Add(Translate("Benchmark"), delegate(JobMonitor monitor)
			{
				Action<BenchmarkResult> callback = delegate(BenchmarkResult benchmark)
				{
					base.Dispatcher.Async(delegate
					{
						RefreshControls(monitor, benchmark);
					});
				};
				GitCommandResult<BenchmarkResult> response = new BenchmarkGitCommand().Execute(gitModule, monitor, callback);
				if (response.Succeeded)
				{
					base.Dispatcher.Async(delegate
					{
						_benchmarkLog = response.Result.BenchmarkLog;
						ProgressBar.Hide();
						Brush scoreBrush = GetScoreBrush(response.Result.Score, 20.0, 60.0);
						ScoreTextBlock.Foreground = scoreBrush;
						ScoreTextBlock.Text = $"{response.Result.Score:0.0}";
						ScoreProgressBar.Show();
						ScoreProgressBar.Value = response.Result.Score.GetValueOrDefault();
						ScoreProgressBar.Foreground = scoreBrush;
						RefreshButton.Show();
						CopyReportButton.Show();
					});
				}
				else if (!monitor.IsCanceled)
				{
					base.Dispatcher.Async(delegate
					{
						new ErrorWindow(repositoryUserControl, response.Error).ShowDialog();
					});
				}
			});
		}

		private void RefreshControls([Null] JobMonitor monitor, [Null] BenchmarkResult benchmark)
		{
			SystemLatencyTextBlock.Foreground = GetElapsedBrush(benchmark?.SystemLatency, 70.0, 100.0);
			SystemLatencyTextBlock.Text = ((benchmark != null && benchmark.SystemLatency.HasValue) ? $"{benchmark.SystemLatency:0.000}s" : "--");
			StatusTextBlock.Foreground = GetElapsedBrush(benchmark?.Status, 0.3, 0.6);
			StatusTextBlock.Text = ((benchmark != null && benchmark.Status.HasValue) ? $"{benchmark.Status:0.000}s" : "--");
			ReferencesTextBlock.Foreground = GetElapsedBrush(benchmark?.References, 0.25, 0.5);
			ReferencesTextBlock.Text = ((benchmark != null && benchmark.References.HasValue) ? $"{benchmark.References:0.000}s" : "--");
			RevisionsTextBlock.Foreground = GetElapsedBrush(benchmark?.Revisions, 0.3, 0.6);
			RevisionsTextBlock.Text = ((benchmark != null && benchmark.Revisions.HasValue) ? $"{benchmark.Revisions:0.000}s" : "--");
			ProgressBar.Show();
			ProgressBar.Value = (monitor?.Progress).GetValueOrDefault();
			RefreshButton.Hide();
			CopyReportButton.Hide();
			ScoreProgressBar.Hide();
			ScoreTextBlock.Text = "--";
			ScoreTextBlock.Foreground = Theme.SecondaryLabelBrush;
		}

		private static Brush GetElapsedBrush(double? value, double low, double high)
		{
			if (value.HasValue)
			{
				double valueOrDefault = value.GetValueOrDefault();
				if (valueOrDefault < low)
				{
					return Theme.ApplicationColors.GreenBrush;
				}
				if (valueOrDefault < high)
				{
					return Theme.ApplicationColors.YellowBrush;
				}
				return Theme.ApplicationColors.RedBrush;
			}
			return Theme.SecondaryLabelBrush;
		}

		private static Brush GetScoreBrush(double? value, double low, double high)
		{
			if (value.HasValue)
			{
				double valueOrDefault = value.GetValueOrDefault();
				if (valueOrDefault < low)
				{
					return Theme.ApplicationColors.RedBrush;
				}
				if (valueOrDefault < high)
				{
					return Theme.ApplicationColors.YellowBrush;
				}
				return Theme.ApplicationColors.GreenBrush;
			}
			return Theme.LabelBrush;
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
