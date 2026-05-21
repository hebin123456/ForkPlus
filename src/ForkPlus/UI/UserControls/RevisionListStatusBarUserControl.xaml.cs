using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.UserControls
{
	public partial class RevisionListStatusBarUserControl : UserControl
	{

		public RepositoryUserControl RepositoryUserControl { get; set; }

		private double StatusBarTextBlockMaxWidth => Container.ActualWidth - 145.0;

		public RevisionListStatusBarUserControl()
		{
			InitializeComponent();
			WeakEventManager<NotificationCenter, RepositoryDataUpdatedEventArgs>.AddHandler(NotificationCenter.Current, "RepositoryDataUpdated", RepositoryDataUpdated);
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
			if (ReferencesTextBlock.ActualWidth > 0.0 && ReferencesTextBlock.ActualWidth > StatusBarTextBlockMaxWidth)
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
