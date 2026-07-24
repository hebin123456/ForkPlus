// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia + using Avalonia.Interactivity（RoutedEventArgs）
// - using System.Windows.Controls → using Avalonia.Controls（UserControl）
// - using System.Windows.Markup → 移除
// - WeakEventManager<NotificationCenter,RepositoryDataUpdatedEventArgs>.AddHandler(...,"RepositoryDataUpdated",h)
//   → NotificationCenter.Current.RepositoryDataUpdated += h（直接订阅，参考 StatisticsUserControl 的 ApplicationThemeChanged）
// - ActualWidth → Bounds.Width（参考 RevisionDetailsUserControl）
using System;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.UserControls
{
	public partial class RevisionListStatusBarUserControl : UserControl
	{

		public RepositoryUserControl RepositoryUserControl { get; set; }

		// 阶段 4.5：WPF ActualWidth → Avalonia Bounds.Width（参考 RevisionDetailsUserControl）。
		private double StatusBarTextBlockMaxWidth => Container.Bounds.Width - 145.0;

		public RevisionListStatusBarUserControl()
		{
			InitializeComponent();
			// 阶段 4.5：WeakEventManager → 直接事件订阅（参考 StatisticsUserControl）。
			NotificationCenter.Current.RepositoryDataUpdated += RepositoryDataUpdated;
			base.SizeChanged += delegate
			{
				InvalidateStatusBarTextBlockMeasurement();
			};
		}

		private void RepositoryDataUpdated(object sender, RepositoryDataUpdatedEventArgs args)
		{
			if (args.RepositoryUserControl != RepositoryUserControl)
			{
				return;
			}
			RepositoryData repositoryData = RepositoryUserControl.RepositoryData;
			if (repositoryData != null)
			{
				if (repositoryData.Reflog)
				{
					HeaderTextBlock.Text = Translate("Reflog mode enabled");
					StatusBarButton.Content = Translate("Exit");
					ReferencesTextBlock.Text = "";
					InvalidateStatusBarTextBlockMeasurement();
					this.Show();
				}
				else if (repositoryData.References.FilterReferences.Length != 0)
				{
					HeaderTextBlock.Text = Translate("Filtered by:");
					ReferencesTextBlock.Text = string.Join(", ", repositoryData.References.FilterReferences.Select(ToFriendlyName));
					StatusBarButton.Content = Translate("Clear filter");
					InvalidateStatusBarTextBlockMeasurement();
					this.Show();
				}
				else
				{
					this.Collapse();
				}
			}
		}

		private void InvalidateStatusBarTextBlockMeasurement()
		{
			// 阶段 4.5：WPF ActualWidth → Avalonia Bounds.Width（参考 RevisionDetailsUserControl）。
			if (ReferencesTextBlock.Bounds.Width > 0.0 && ReferencesTextBlock.Bounds.Width > StatusBarTextBlockMaxWidth)
			{
				ReferencesTextBlock.MaxWidth = StatusBarTextBlockMaxWidth;
				ReferencesTextBlock.InvalidateMeasure();
			}
		}

		private string ToFriendlyName(string fullReference)
		{
			fullReference = fullReference.Replace("refs/heads/", "");
			fullReference = fullReference.Replace("refs/remotes/", "");
			fullReference = fullReference.Replace("refs/tags/", "");
			if (fullReference.EndsWith("/"))
			{
				return "'" + fullReference + "*'";
			}
			return "'" + fullReference + "'";
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

		private void StatusBarButton_Click(object sender, RoutedEventArgs e)
		{
			RepositoryUserControl repositoryUserControl = RepositoryUserControl;
			if (repositoryUserControl == null)
			{
				return;
			}
			RepositoryData repositoryData = repositoryUserControl.RepositoryData;
			if (repositoryData != null)
			{
				if (repositoryData.Reflog)
				{
					RepositoryUserControl.Commands.ToggleShowReflogInRevisionList.Execute();
				}
				else
				{
					RepositoryUserControl.Commands.UpdateReferenceFilter.ClearFilter(repositoryUserControl);
				}
			}
		}

	}
}
