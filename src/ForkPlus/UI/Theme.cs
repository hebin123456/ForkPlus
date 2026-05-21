using System;
using System.Windows;
using System.Windows.Media;

namespace ForkPlus.UI
{
	internal static class Theme
	{
		public static class CommandTextBox
		{
			public static Brush LabelBackgroundBrush => FindBrush("CommandTextBox.LabelBackground");

			public static Brush LabelForegroundBrush => FindBrush("CommandTextBox.LabelForeground");
		}

		public static class CodeEditor
		{
			public static Brush BackgroundBrush => FindBrush("CodeEditorBackground");
		}

		public static class FileListMultiselectionTreeView
		{
			public static Style DefaultStyle => FindStyle("FileListMultiselectionTreeViewDefaultStyle");

			public static Style GridViewStyle => FindStyle("FileListMultiselectionTreeViewWithGridViewStyle");
		}

		public static class CommitUserControl
		{
			public static Style CommitButtonVisibleDropdownStyle => FindStyle("CommitButtonVisibleDropdownStyle");

			public static Style CommitButtonHiddenDropdownStyle => FindStyle("CommitButtonHiddenDropdownStyle");
		}

		public static class Diff
		{
			public static Brush FloatingButtonContainerBackground => FindBrush("Diff.FloatingButtonContainer.Background");

			public static Brush AddedForegroundBrush => FindBrush("Diff.Added.Foreground");

			public static Brush AddedBrush => FindBrush("Diff.Added");

			public static Brush RemovedForegroundBrush => FindBrush("Diff.Removed.Foreground");

			public static Brush RemovedBrush => FindBrush("Diff.Removed");
		}

		public static class ApplicationColors
		{
			public static Brush GrayBrush => FindBrush("InteractiveRebase.Gray");

			public static Brush GreenBrush => FindBrush("InteractiveRebase.Green");

			public static Brush RedBrush => FindBrush("InteractiveRebase.Red");

			public static Brush YellowBrush => FindBrush("InteractiveRebase.Yellow");
		}

		public static class RevisionTimeLine
		{
			public static Brush BackgroundBrush => FindBrush("Item.Static.Background");

			public static Brush LabelBrush => FindBrush("RevisionTimeLine.LabelBrush");

			public static Brush RevisionBrush => FindBrush("RevisionTimeLine.RevisionBrush");

			public static Brush TickBrush => FindBrush("RevisionTimeLine.TickBrush");

			public static Brush AlternationBrush => FindBrush("RevisionTimeLine.AlternationBrush");
		}

		public static class RevisionList
		{
			public static Brush ItemSelectedInactiveBackgroundBrush => FindBrush("Item.SelectedInactive.Background");

			public static Brush ItemBackgroundBrush => FindBrush("ListBox.Static.Background");
		}

		public enum SystemColorType
		{
			Accent,
			Accent1,
			Accent2
		}

		[Null]
		private static ResourceDictionary _systemAccentBrushes;

		public static Brush SystemAccentBrush => FindBrush("SystemAccentBrush");

		public static Brush AccentBrush => FindBrush("AccentBrush");

		public static Brush BorderBrush => FindBrush("BorderBrush");

		public static Brush BackgroundBrush => FindBrush("BackgroundBrush");

		public static Brush LabelBrush => FindBrush("LabelBrush");

		public static Brush SecondaryLabelBrush => FindBrush("SecondaryLabelBrush");

		public static Brush MergeStatusLabelBrushRed => FindBrush("Merge.StatusLabel.Red");

		public static Brush MergeStatusLabelBrushGreen => FindBrush("Merge.StatusLabel.Green");

		public static Brush HeaderMenuItemBrush => FindBrush("Menu.MenuItem.Disabled.Foreground");

		public static Brush FilterPanelSecondaryBackground => FindBrush("FilterPanel.SecondaryBackground");

		public static Brush FilterPanelSecondaryBorder => FindBrush("FilterPanel.SecondaryBorder");

		public static Brush ForkPlusDialogBackgroundBrush => FindBrush("Window.Dialog.Background");

		public static ImageSource BranchFilterOnIcon => FindImage("BranchFilterOnIcon");

		public static ImageSource BranchFilterOnSelectedIcon => FindImage("BranchFilterOnSelectedIcon");

		public static ImageSource BranchFilterOffIcon => FindImage("BranchFilterOffIcon");

		public static ImageSource BranchFilterOffSelectedIcon => FindImage("BranchFilterOffSelectedIcon");

		public static ImageSource BranchIcon => FindImage("BranchIcon");

		public static ImageSource BranchSelectedIcon => FindImage("BranchSelectedIcon");

		public static ImageSource BranchWarningIcon => FindImage("BranchWarningIcon");

		public static ImageSource BranchWarningSelectedIcon => FindImage("BranchWarningSelectedIcon");

		public static ImageSource BranchPaleIcon => FindImage("BranchPaleIcon");

		public static ImageSource BranchPaleSelectedIcon => FindImage("BranchPaleSelectedIcon");

		public static ImageSource ConsoleIcon => FindImage("ConsoleIcon");

		public static ImageSource HideBranchOnIcon => FindImage("HideBranchOnIcon");

		public static ImageSource HideBranchOffIcon => FindImage("HideBranchOffIcon");

		public static ImageSource LockIcon => FindImage("LockIcon");

		public static ImageSource OpenInIcon => FindImage("OpenInIcon");

		public static ImageSource PinOnIcon => FindImage("PinOnIcon");

		public static ImageSource PinOffIcon => FindImage("PinOffIcon");

		public static ImageSource RevisionIcon => FindImage("RevisionIcon");

		public static ImageSource StashIcon => FindImage("SidebarStashIcon");

		public static ImageSource TagIcon => FindImage("TagIcon");

		public static ImageSource UnlockIcon => FindImage("UnlockIcon");

		public static ImageSource AzureIcon => FindImage("AzureIcon");

		public static ImageSource AzureOnIcon => FindImage("AzureOnIcon");

		public static ImageSource BitbucketIcon => FindImage("BitbucketIcon");

		public static ImageSource BitbucketOnIcon => FindImage("BitbucketOnIcon");

		public static ImageSource GitHubIcon => FindImage("GitHubIcon");

		public static ImageSource GitHubOnIcon => FindImage("GitHubOnIcon");

		public static ImageSource GitLabIcon => FindImage("GitLabIcon");

		public static ImageSource GitLabOnIcon => FindImage("GitLabOnIcon");

		public static ImageSource GiteaIcon => FindImage("GiteaIcon");

		public static ImageSource GiteaOnIcon => FindImage("GiteaOnIcon");

		public static ImageSource RemoteIcon => FindImage("GenericRemoteIcon");

		public static ImageSource RemoteOnIcon => FindImage("GenericRemoteOnIcon");

		public static ImageSource IssueIcon => FindImage("IssueIcon");

		public static ImageSource PullRequestIcon => FindImage("PullRequestIcon");

		public static ImageSource RepositoryIcon => FindImage("RepositoryIcon");

		public static ImageSource RepositoryWarningIcon => FindImage("RepositoryWarningIcon");

		public static ImageSource HorizontalMergerIcon => FindImage("HorizontalMergerIcon");

		public static ImageSource VerticalMergerIcon => FindImage("VerticalMergerIcon");

		public static ImageSource FolderIcon => FindImage("FolderIcon");

		public static ImageSource WarningIcon => FindImage("WarningIcon");

		public static Geometry AzureGeometry => FindGeometry("AzureGeometry");

		public static Geometry BitbucketGeometry => FindGeometry("BitbucketGeometry");

		public static Geometry GitHubGeometry => FindGeometry("GitHubGeometry");

		public static Geometry GitLabGeometry => FindGeometry("GitLabGeometry");

		public static Geometry GiteaGeometry => FindGeometry("GiteaGeometry");

		public static Geometry RemoteGeometry => FindGeometry("GenericRemoteGeometry");

		public static Style BranchOptionButtonStyle => FindStyle("BranchOptionButton");

		public static Style CustomContentMenuItemStyle => FindStyle("CustomContentMenuItemStyle");

		public static Style SidebarTabButtonPathStyle => FindStyle("SidebarTabButtonPath");

		public static Style TransparentButtonStyle => FindStyle("TransparentButtonStyle");

		public static ScaleTransform LayoutScaleTransform => FindTransform("LayoutScaleTransform");

		public static ImageSource FindImage(string resourceKey)
		{
			return FindResource(resourceKey) as ImageSource;
		}

		public static Geometry FindGeometry(string resourceKey)
		{
			return FindResource(resourceKey) as Geometry;
		}

		public static Brush FindBrush(string resourceKey)
		{
			return FindResource(resourceKey) as Brush;
		}

		public static Style FindStyle(string resourceKey)
		{
			return FindResource(resourceKey) as Style;
		}

		public static ScaleTransform FindTransform(string resourceKey)
		{
			return FindResource(resourceKey) as ScaleTransform;
		}

		public static object FindResource(string resourceKey)
		{
			return Application.Current.TryFindResource(resourceKey);
		}

		public static void SubscribeToSystemEvents()
		{
			if (App.OSVersion.Major >= new Version(10, 0).Major)
			{
				SystemThemeHelper.SubscribeToSystemEvents();
			}
		}

		public static void Refresh()
		{
			Log.Info("Refresh Theme");
			ResourceDictionary resourceDictionary = new ResourceDictionary();
			resourceDictionary.Add("SystemAccentBrush", GetSystemBrush(SystemColorType.Accent2, AccentBrush));
			Application.Current.Resources.MergedDictionaries.Add(resourceDictionary);
			if (_systemAccentBrushes != null)
			{
				Application.Current.Resources.MergedDictionaries.Remove(_systemAccentBrushes);
			}
			_systemAccentBrushes = resourceDictionary;
		}

		private static Brush GetSystemBrush(SystemColorType colorType, Brush fallback)
		{
			if (App.OSVersion.Major < new Version(10, 0).Major)
			{
				return fallback;
			}
			return SystemThemeHelper.GetSystemBrush(colorType) ?? fallback;
		}
	}
}
