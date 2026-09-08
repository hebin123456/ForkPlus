using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

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
			public static global::Avalonia.Styling.IStyle DefaultStyle => FindStyle("FileListMultiselectionTreeViewDefaultStyle");

			public static global::Avalonia.Styling.IStyle GridViewStyle => FindStyle("FileListMultiselectionTreeViewWithGridViewStyle");
		}

		public static class CommitUserControl
		{
			public static global::Avalonia.Styling.IStyle CommitButtonVisibleDropdownStyle => FindStyle("CommitButtonVisibleDropdownStyle");

			public static global::Avalonia.Styling.IStyle CommitButtonHiddenDropdownStyle => FindStyle("CommitButtonHiddenDropdownStyle");
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

		public static global::Avalonia.Media.IImage BranchFilterOnIcon => FindImage("BranchFilterOnIcon");

		public static global::Avalonia.Media.IImage BranchFilterOnSelectedIcon => FindImage("BranchFilterOnSelectedIcon");

		public static global::Avalonia.Media.IImage BranchFilterOffIcon => FindImage("BranchFilterOffIcon");

		public static global::Avalonia.Media.IImage BranchFilterOffSelectedIcon => FindImage("BranchFilterOffSelectedIcon");

		public static global::Avalonia.Media.IImage BranchIcon => FindImage("BranchIcon");

		public static global::Avalonia.Media.IImage BranchSelectedIcon => FindImage("BranchSelectedIcon");

		public static global::Avalonia.Media.IImage BranchWarningIcon => FindImage("BranchWarningIcon");

		public static global::Avalonia.Media.IImage BranchWarningSelectedIcon => FindImage("BranchWarningSelectedIcon");

		public static global::Avalonia.Media.IImage BranchPaleIcon => FindImage("BranchPaleIcon");

		public static global::Avalonia.Media.IImage BranchPaleSelectedIcon => FindImage("BranchPaleSelectedIcon");

		public static global::Avalonia.Media.IImage ConsoleIcon => FindImage("ConsoleIcon");

		public static global::Avalonia.Media.IImage HideBranchOnIcon => FindImage("HideBranchOnIcon");

		public static global::Avalonia.Media.IImage HideBranchOffIcon => FindImage("HideBranchOffIcon");

		public static global::Avalonia.Media.IImage LockIcon => FindImage("LockIcon");

		public static global::Avalonia.Media.IImage OpenInIcon => FindImage("OpenInIcon");

		public static global::Avalonia.Media.IImage PinOnIcon => FindImage("PinOnIcon");

		public static global::Avalonia.Media.IImage PinOffIcon => FindImage("PinOffIcon");

		public static global::Avalonia.Media.IImage RevisionIcon => FindImage("RevisionIcon");

		public static global::Avalonia.Media.IImage StashIcon => FindImage("SidebarStashIcon");

		public static global::Avalonia.Media.IImage TagIcon => FindImage("TagIcon");

		public static global::Avalonia.Media.IImage UnlockIcon => FindImage("UnlockIcon");

		public static global::Avalonia.Media.IImage AzureIcon => FindImage("AzureIcon");

		public static global::Avalonia.Media.IImage AzureOnIcon => FindImage("AzureOnIcon");

		public static global::Avalonia.Media.IImage BitbucketIcon => FindImage("BitbucketIcon");

		public static global::Avalonia.Media.IImage BitbucketOnIcon => FindImage("BitbucketOnIcon");

		public static global::Avalonia.Media.IImage GitHubIcon => FindImage("GitHubIcon");

		public static global::Avalonia.Media.IImage GitHubOnIcon => FindImage("GitHubOnIcon");

		public static global::Avalonia.Media.IImage GitLabIcon => FindImage("GitLabIcon");

		public static global::Avalonia.Media.IImage GitLabOnIcon => FindImage("GitLabOnIcon");

		public static global::Avalonia.Media.IImage GiteaIcon => FindImage("GiteaIcon");

		public static global::Avalonia.Media.IImage GiteaOnIcon => FindImage("GiteaOnIcon");

		public static global::Avalonia.Media.IImage RemoteIcon => FindImage("GenericRemoteIcon");

		public static global::Avalonia.Media.IImage RemoteOnIcon => FindImage("GenericRemoteOnIcon");

		public static global::Avalonia.Media.IImage IssueIcon => FindImage("IssueIcon");

		public static global::Avalonia.Media.IImage PullRequestIcon => FindImage("PullRequestIcon");

		public static global::Avalonia.Media.IImage RepositoryIcon => FindImage("RepositoryIcon");

		public static global::Avalonia.Media.IImage RepositoryWarningIcon => FindImage("RepositoryWarningIcon");

		public static global::Avalonia.Media.IImage HorizontalMergerIcon => FindImage("HorizontalMergerIcon");

		public static global::Avalonia.Media.IImage VerticalMergerIcon => FindImage("VerticalMergerIcon");

		public static global::Avalonia.Media.IImage FolderIcon => FindImage("FolderIcon");

		public static global::Avalonia.Media.IImage WarningIcon => FindImage("WarningIcon");

		public static Geometry AzureGeometry => FindGeometry("AzureGeometry");

		public static Geometry BitbucketGeometry => FindGeometry("BitbucketGeometry");

		public static Geometry GitHubGeometry => FindGeometry("GitHubGeometry");

		public static Geometry GitLabGeometry => FindGeometry("GitLabGeometry");

		public static Geometry GiteaGeometry => FindGeometry("GiteaGeometry");

		public static Geometry RemoteGeometry => FindGeometry("GenericRemoteGeometry");

		public static global::Avalonia.Styling.IStyle BranchOptionButtonStyle => FindStyle("BranchOptionButton");

		public static global::Avalonia.Styling.IStyle CustomContentMenuItemStyle => FindStyle("CustomContentMenuItemStyle");

		public static global::Avalonia.Styling.IStyle SidebarTabButtonPathStyle => FindStyle("SidebarTabButtonPath");

		public static global::Avalonia.Styling.IStyle TransparentButtonStyle => FindStyle("TransparentButtonStyle");

		public static ScaleTransform LayoutScaleTransform => FindTransform("LayoutScaleTransform");

		public static global::Avalonia.Media.IImage FindImage(string resourceKey)
                {
                        // Migration note：WPF ImageSource → Avalonia IImage（资源里的位图对象）。
                        return (global::Avalonia.Application.Current != null && global::Avalonia.Application.Current.TryGetResource(resourceKey, global::Avalonia.Application.Current.ActualThemeVariant, out var __res0) ? __res0 as global::Avalonia.Media.IImage : null);
                }

		public static Geometry FindGeometry(string resourceKey)
		{
			return (global::Avalonia.Application.Current != null && global::Avalonia.Application.Current.TryGetResource(resourceKey, global::Avalonia.Application.Current.ActualThemeVariant, out var __res1) ? __res1 as Geometry : null);
		}

		public static Brush FindBrush(string resourceKey)
		{
			return (global::Avalonia.Application.Current != null && global::Avalonia.Application.Current.TryGetResource(resourceKey, global::Avalonia.Application.Current.ActualThemeVariant, out var __res2) ? __res2 as Brush : null);
		}

		public static global::Avalonia.Styling.IStyle FindStyle(string resourceKey)
		{
			// Migration note：主题资源（x:Key 的 Style）迁移后全部是 ControlTheme，与 Style 互不继承，
			// 原 `as Style` 恒得 null（运行时 ControlTheme 再被 Styles.Add(null) 炸 NRE）。
			// 返回 IStyle（ControlTheme/Style 公共接口）；ControlTheme 消费方须经 StyleCompat.SetStyle
			// 挂到 TemplatedControl.Theme。
			return (global::Avalonia.Application.Current != null && global::Avalonia.Application.Current.TryGetResource(resourceKey, global::Avalonia.Application.Current.ActualThemeVariant, out var __res3) ? __res3 as global::Avalonia.Styling.IStyle : null);
		}

		public static ScaleTransform FindTransform(string resourceKey)
		{
			return (global::Avalonia.Application.Current != null && global::Avalonia.Application.Current.TryGetResource(resourceKey, global::Avalonia.Application.Current.ActualThemeVariant, out var __res4) ? __res4 as ScaleTransform : null);
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
