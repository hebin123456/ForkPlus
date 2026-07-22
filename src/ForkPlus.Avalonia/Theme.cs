using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

// Avalonia spike 版 Theme（namespace ForkPlus.Avalonia）。
//
// 对照 WPF 工程 src/ForkPlus/UI/Theme.cs（276 行）：
//   - WPF: internal static class Theme
//   - 大量静态属性：嵌套类（CommandTextBox/CodeEditor/FileListMultiselectionTreeView/
//     CommitUserControl/Diff/ApplicationColors/RevisionTimeLine/RevisionList）+ 顶层
//     Brush/Style/ImageSource/Geometry/ScaleTransform 资源访问器
//   - FindResource: Application.Current.TryFindResource(resourceKey)（WPF 返回 object）
//   - FindBrush/FindImage/FindGeometry/FindStyle/FindTransform: as 转换
//   - SubscribeToSystemEvents/Refresh/GetSystemBrush: 系统强调色（WinRT UISettings，Win10+）
//   - 依赖：System.Windows（Application/ResourceDictionary/Style/ScaleTransform）+
//     System.Windows.Media（Brush/ImageSource/Geometry）+ App.OSVersion + SystemThemeHelper
//
// Avalonia 版差异（spike 简化策略，task spec：主题资源管理）：
//   1. WPF Brush → Avalonia.Media.IBrush（资源为 ISolidColorBrush）
//   2. WPF ImageSource → Avalonia.Media.IImage
//   3. WPF Geometry → Avalonia.Media.Geometry（同名）
//   4. WPF Style → Avalonia.Styling.Style（同名，需 using Avalonia.Styling）
//   5. WPF ScaleTransform → Avalonia.Media.ScaleTransform（同名）
//   6. WPF Application.Current.TryFindResource(key) 返回 object →
//      Avalonia ResourceNodeExtensions.TryFindResource(host, key, out value) 返回 bool
//      （扩展方法在 Avalonia.Controls.ResourceNodeExtensions，需 using Avalonia.Controls）
//   7. WPF Refresh 用 ResourceDictionary + MergedDictionaries 交换 →
//      spike 简化为 Application.Current.Resources[key]=value 直接覆盖
//   8. WPF App.OSVersion.Major >= 10 守卫 → spike 移除（跨平台，直接调用 SystemThemeHelper）
//   9. WPF [Null] Attribute → spike 跳过（nullable disable in csproj）
//
// spike 简化（task spec 关键 API）：
//   - 保留全部嵌套类 + 顶层资源属性（IBrush/Style/IImage/Geometry/ScaleTransform）
//   - FindResource/FindBrush/FindImage/FindGeometry/FindStyle/FindTransform
//   - SubscribeToSystemEvents/Refresh/GetSystemBrush（简化）
namespace ForkPlus.Avalonia
{
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

		public enum SystemColorType
		{
			Accent,
			Accent1,
			Accent2
		}

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
			if (Application.Current != null && Application.Current.TryGetResource(resourceKey, null, out object value))
			{
				return value;
			}
			return null;
		}

		public static void SubscribeToSystemEvents()
		{
			// spike 版：移除 WPF App.OSVersion.Major >= 10 守卫（跨平台），直接委托 SystemThemeHelper
			SystemThemeHelper.SubscribeToSystemEvents();
		}

		public static void Refresh()
		{
			Log.Info("Refresh Theme");
			// spike 版：WPF 用 ResourceDictionary + MergedDictionaries 交换系统强调色，
			// Avalonia 简化为直接覆盖 Application.Resources["SystemAccentBrush"]
			var app = Application.Current;
			if (app != null)
			{
				app.Resources["SystemAccentBrush"] = GetSystemBrush(SystemColorType.Accent2, AccentBrush);
			}
		}

		private static IBrush GetSystemBrush(SystemColorType colorType, IBrush fallback)
		{
			// spike 版：SystemThemeHelper.GetSystemBrush 返回 null（spike 不读系统强调色），回退到 fallback
			return SystemThemeHelper.GetSystemBrush(colorType.ToString()) ?? fallback;
		}
	}
}
