using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// WS2.3（丢弃文件对话框化）：替代 DiscardChangedFilesCommand 原 MessageBox 确认——
	/// 内容区分组（子仓/普通文件）列出将丢弃的路径，长列表 ScrollViewer 滚动，单组超过
	/// <see cref="MaxDisplayedPaths"/> 条截断并显示"…and {0} more"。确认语义与原 MessageBox
	/// 一致：OnSubmit 关闭返回 true，调用方据此执行原丢弃逻辑；分组只是展示，不影响丢弃集合
	/// （子仓仍由原命令在文件丢弃后经 DiscardSubmoduleChangesCommand 处理）。
	/// </summary>
	public partial class DiscardChangesWindow : ForkPlusDialogWindow
	{
		/// <summary>单组（子仓/文件）列表显示上限，超出部分以"…and {0} more"汇总。</summary>
		public const int MaxDisplayedPaths = 200;

		public DiscardChangesWindow(ChangedFile[] changedFiles, Submodule[] submodules, string submitButtonTitle)
		{
			InitializeComponent();
			// 原 MessageBox 的标题/描述/按钮文案（行为语义不变，含本地化键）
			base.DialogTitle = Translate("Discard changes");
			base.DialogDescription = Translate("Do you want to discard all your changes in the selected files?");
			base.SubmitButtonTitle = Translate(submitButtonTitle ?? "Discard");
			FillGroup(SubmodulesHeaderTextBlock, SubmodulesItemsControl, SubmodulesOverflowTextBlock,
				"Submodules", (submodules ?? Array.Empty<Submodule>()).Map((Submodule x) => x.Path));
			FillGroup(FilesHeaderTextBlock, FilesItemsControl, FilesOverflowTextBlock,
				"Files", (changedFiles ?? Array.Empty<ChangedFile>()).Map((ChangedFile x) => x.Path));
		}

		protected override void OnSubmit()
		{
			CloseWithOk();
		}

		private void FillGroup(TextBlock header, ItemsControl itemsControl, TextBlock overflow, string headerKey, string[] paths)
		{
			if (paths == null || paths.Length == 0)
			{
				return;
			}
			header.Text = Translate(headerKey);
			header.IsVisible = true;
			itemsControl.ItemsSource = paths.Take(Math.Min(paths.Length, MaxDisplayedPaths)).ToArray();
			itemsControl.IsVisible = true;
			if (paths.Length > MaxDisplayedPaths)
			{
				overflow.Text = string.Format(Translate("...and {0} more"), paths.Length - MaxDisplayedPaths);
				overflow.IsVisible = true;
			}
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}
