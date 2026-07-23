using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using ForkPlus.Services;

namespace ForkPlus.UI
{
	/// <summary>
	/// 阶段 4 里程碑 4.3：Theme 静态资源访问门面 WPF→Avalonia 迁移。
	/// Application.Current.TryFindResource → Application.Current.FindResource。
	/// WPF ResourceDictionary.MergedDictionaries.Add/Remove 强制刷新 → 转发到 IThemeService.Refresh。
	/// Brush/ImageSource/Geometry/Style/ScaleTransform → Avalonia.Media/Styling 等价类型。
	/// </summary>
	internal static class Theme
	{
		public static class CommandTextBox
		{
			public static IBrush LabelBackgroundBrush => FindBrush("CommandTextBox.LabelBackground");

			public static IBrush LabelForegroundBrush => FindBrush("CommandTextBox.LabelForeground");
		}

		public static class CodeEditor
		{
			public static IBrush BackgroundBrush => FindBrush("CodeEditorBackground");
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
			public static IBrush FloatingButtonContainerBackground => FindBrush("Diff.FloatingButtonContainer.Background");

			public static IBrush AddedForegroundBrush => FindBrush("Diff.Added.Foreground");

			public static IBrush AddedBrush => FindBrush("Diff.Added");

			public static IBrush RemovedForegroundBrush => FindBrush("Diff.Removed.Foreground");

			public static IBrush RemovedBrush => FindBrush("Diff.Removed");
		}

		public static class ApplicationColors
		{
			public static IBrush GrayBrush => FindBrush("InteractiveRebase.Gray");

			public static IBrush GreenBrush => FindBrush("InteractiveRebase.Green");

			public static IBrush RedBrush => FindBrush("InteractiveRebase.Red");

			public static IBrush YellowBrush => FindBrush("InteractiveRebase.Yellow");
		}

		public static class RevisionTimeLine
		{
			public static IBrush BackgroundBrush => FindBrush("Item.Static.Background");

			public static IBrush LabelBrush => FindBrush("RevisionTimeLine.LabelBrush");

			public static IBrush RevisionBrush => FindBrush("RevisionTimeLine.RevisionBrush");

			public static IBrush TickBrush => FindBrush("RevisionTimeLine.TickBrush");

			public static IBrush AlternationBrush => FindBrush("RevisionTimeLine.AlternationBrush");
		}

		public static class RevisionList
		{
			public static IBrush ItemSelectedInactiveBackgroundBrush => FindBrush("Item.SelectedInactive.Background");

			public static IBrush ItemBackgroundBrush => FindBrush("ListBox.Static.Background");
		}

		// SystemColorType 已提取到 Services/IThemeService.cs，供 IThemeService 引用。

		public static IBrush SystemAccentBrush => FindBrush("SystemAccentBrush");

		public static IBrush AccentBrush => FindBrush("AccentBrush");

		public static IBrush BorderBrush => FindBrush("BorderBrush");

		public static IBrush BackgroundBrush => FindBrush("BackgroundBrush");

		public static IBrush LabelBrush => FindBrush("LabelBrush");

		public static IBrush SecondaryLabelBrush => FindBrush("SecondaryLabelBrush");

		public static IBrush MergeStatusLabelBrushRed => FindBrush("Merge.StatusLabel.Red");

		public static IBrush MergeStatusLabelBrushGreen => FindBrush("Merge.StatusLabel.Green");

		public static IBrush HeaderMenuItemBrush => FindBrush("Menu.MenuItem.Disabled.Foreground");

		public static IBrush FilterPanelSecondaryBackground => FindBrush("FilterPanel.SecondaryBackground");

		public static IBrush FilterPanelSecondaryBorder => FindBrush("FilterPanel.SecondaryBorder");

		public static IBrush ForkPlusDialogBackgroundBrush => FindBrush("Window.Dialog.Background");

		public static IImage BranchFilterOnIcon => FindImage("BranchFilterOnIcon");

		public static IImage BranchFilterOnSelectedIcon => FindImage("BranchFilterOnSelectedIcon");

		public static IImage BranchFilterOffIcon => FindImage("BranchFilterOffIcon");

		public static IImage BranchFilterOffSelectedIcon => FindImage("BranchFilterOffSelectedIcon");

		public static IImage BranchIcon => FindImage("BranchIcon");

		public static IImage BranchSelectedIcon => FindImage("BranchSelectedIcon");

		public static IImage BranchWarningIcon => FindImage("BranchWarningIcon");

		public static IImage BranchWarningSelectedIcon => FindImage("BranchWarningSelectedIcon");

		public static IImage BranchPaleIcon => FindImage("BranchPaleIcon");

		public static IImage BranchPaleSelectedIcon => FindImage("BranchPaleSelectedIcon");

		public static IImage ConsoleIcon => FindImage("ConsoleIcon");

		public static IImage HideBranchOnIcon => FindImage("HideBranchOnIcon");

		public static IImage HideBranchOffIcon => FindImage("HideBranchOffIcon");

		public static IImage LockIcon => FindImage("LockIcon");

		public static IImage OpenInIcon => FindImage("OpenInIcon");

		public static IImage PinOnIcon => FindImage("PinOnIcon");

		public static IImage PinOffIcon => FindImage("PinOffIcon");

		public static IImage RevisionIcon => FindImage("RevisionIcon");

		public static IImage StashIcon => FindImage("SidebarStashIcon");

		public static IImage TagIcon => FindImage("TagIcon");

		public static IImage UnlockIcon => FindImage("UnlockIcon");

		public static IImage AzureIcon => FindImage("AzureIcon");

		public static IImage AzureOnIcon => FindImage("AzureOnIcon");

		public static IImage BitbucketIcon => FindImage("BitbucketIcon");

		public static IImage BitbucketOnIcon => FindImage("BitbucketOnIcon");

		public static IImage GitHubIcon => FindImage("GitHubIcon");

		public static IImage GitHubOnIcon => FindImage("GitHubOnIcon");

		public static IImage GitLabIcon => FindImage("GitLabIcon");

		public static IImage GitLabOnIcon => FindImage("GitLabOnIcon");

		public static IImage GiteaIcon => FindImage("GiteaIcon");

		public static IImage GiteaOnIcon => FindImage("GiteaOnIcon");

		public static IImage RemoteIcon => FindImage("GenericRemoteIcon");

		public static IImage RemoteOnIcon => FindImage("GenericRemoteOnIcon");

		public static IImage IssueIcon => FindImage("IssueIcon");

		public static IImage PullRequestIcon => FindImage("PullRequestIcon");

		public static IImage RepositoryIcon => FindImage("RepositoryIcon");

		public static IImage RepositoryWarningIcon => FindImage("RepositoryWarningIcon");

		public static IImage HorizontalMergerIcon => FindImage("HorizontalMergerIcon");

		public static IImage VerticalMergerIcon => FindImage("VerticalMergerIcon");

		public static IImage FolderIcon => FindImage("FolderIcon");

		public static IImage WarningIcon => FindImage("WarningIcon");

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

		public static IImage FindImage(string resourceKey)
		{
			return FindResource(resourceKey) as IImage;
		}

		public static Geometry FindGeometry(string resourceKey)
		{
			return FindResource(resourceKey) as Geometry;
		}

		public static IBrush FindBrush(string resourceKey)
		{
			return FindResource(resourceKey) as IBrush;
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
			// Avalonia: Application.Current.FindResource 在资源不存在时抛异常，
			// 用 TryGetResource 安全查找（Avalonia 11 API）。
			if (Application.Current?.Resources.TryGetResource(resourceKey, out object value) == true)
			{
				return value;
			}
			return null;
		}

		public static void SubscribeToSystemEvents()
		{
			// 系统主题事件订阅转发到 ISystemThemeService
			ServiceLocator.SystemTheme?.SubscribeToSystemEvents();
		}

		public static void Refresh()
		{
			Log.Info("Refresh Theme");
			// WPF MergedDictionaries.Add/Remove 强制刷新机制 → 转发到 IThemeService
			ServiceLocator.ThemeService?.Refresh();
		}

		internal static IBrush GetSystemBrush(SystemColorType colorType, IBrush fallback)
		{
			return ServiceLocator.ThemeService?.GetSystemBrush(colorType, fallback) ?? fallback;
		}
	}
}
