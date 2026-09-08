// 回归测试（2026-09-07，"重命名仓库，仓库名还是没有选中整个名字，看不见原来叫什么，
// 只是一个很窄的小框"）：
// 守卫重命名编辑框（EditableTextBlock → CustomAdorner → AdornerLayer）的宽度：
//   根因：进入编辑态时 ETB 模板里的 TextBlock 隐藏（IsVisible 反绑 IsInEditMode），
//   ETB 本身是自动宽度（HorizontalAlignment=Left、无显式 Width，见
//   RepositoryManagerUserControl.axaml 仓库项模板）→ 塌缩为 0 宽；
//   WpfCompat AdornerLayer.RepositionAll() 挂在 TopLevel.LayoutUpdated 上，
//   布局完成后把 adorner.Width 强设为被装饰元素当前的 Bounds.Width（=0）→
//   编辑 TextBox 被 arrange 成 0 宽，只剩 1px 边框的"很窄的小框"。
//   （WPF 原版 AdornerLayer 不强制装饰器尺寸——装饰器自量内容宽，故原版无此问题；
//    既有 RenameEditorStyleTests 显式给 ETB 设 Width=220 恰好绕开了塌缩路径，测不到。）
//   修复：Adorner 增加 TracksAdornedElementSize 开关（默认 true 保持既有
//   DropPlace/DragAndDrop 等装饰器行为），CustomAdorner（仅重命名编辑器使用）
//   关掉——装饰器尺寸来自子控件内容（TextBox 按名字宽度自量），只跟随被装饰元素定位。
using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class RenameEditorWidthTests
	{
		private const string RepoName = "my-repository-name";

		// 与生产 RepositoryManagerUserControl.axaml 仓库项模板同构：
		// 18px 图标列 + Star 文本列，ETB 自动宽度（无显式 Width）。
		private static TextBox EnterEditModeNoExplicitWidth(out Window window)
		{
			var etb = new ForkPlus.UI.Controls.RepositoryManagerEditableTextBlock
			{
				FontSize = 14,
				Height = 22,
				HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
				Value = RepoName
			};
			var row = new Grid { Height = 22, Width = 420 };
			row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
			row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(17) });
			Grid.SetColumn(etb, 1);
			row.Children.Add(etb);
			window = new Window { Width = 460, Height = 120, Content = row };
			window.Show();
			Dispatcher.UIThread.RunJobs();
			etb.IsInEditMode = true;
			// 触发布局收敛：编辑态改变 → ETB 塌缩 → LayoutUpdated → RepositionAll 重设尺寸 → 再布局
			Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
			Task.Delay(100).GetAwaiter().GetResult();
			Dispatcher.UIThread.RunJobs();
			Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
			Task.Delay(50).GetAwaiter().GetResult();
			Dispatcher.UIThread.RunJobs();
			return window.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
		}

		[Fact]
		public void RenameEditor_NoExplicitWidth_EditorBoxStaysNameWide()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				TextBox editor = EnterEditModeNoExplicitWidth(out Window window);
				try
				{
					Assert.NotNull(editor);
					// 修复前：adorner.Width 被强设为塌缩后的 ETB 宽（0）→ TextBox arrange 0 宽
					Assert.True(editor.Bounds.Width > 60,
						$"编辑框宽度 {editor.Bounds.Width} 应按名字内容自量（约 150+），修复前实测为 0-2px 窄条");
					// 全选必须落在渲染层（窄框里看不见选中就是这个根因）
					Avalonia.Controls.Presenters.TextPresenter presenter =
						editor.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.TextPresenter>().First();
					Assert.Equal(0, presenter.SelectionStart);
					Assert.Equal(RepoName.Length, presenter.SelectionEnd);
				}
				finally
				{
					window.Close();
				}
			});
		}
	}
}
